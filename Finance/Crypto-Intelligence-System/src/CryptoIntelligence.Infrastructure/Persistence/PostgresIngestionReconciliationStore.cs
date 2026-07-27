using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Domain.Ingestion;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresIngestionReconciliationStore(
    CryptoIntelligenceDbContext context)
    : IIngestionReconciliationStore
{
    public async Task<IngestionCheckpointSnapshot> GetOrCreateAsync(
        IngestionCheckpointKey key,
        ulong initialThroughSlot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var checkpoint = await FindCheckpointAsync(key, cancellationToken);
        if (checkpoint is not null)
        {
            return ToSnapshot(checkpoint);
        }

        checkpoint = new IngestionCheckpointEntity
        {
            Id = Guid.NewGuid(),
            Chain = key.Chain,
            Network = key.Network,
            Source = key.Source,
            SubscriptionType = key.SubscriptionType,
            ObservedThroughSlot = ToDatabaseSlot(initialThroughSlot),
            PersistedThroughSlot = ToDatabaseSlot(initialThroughSlot),
            ProcessedThroughSlot = ToDatabaseSlot(initialThroughSlot),
            FinalizedThroughSlot = ToDatabaseSlot(initialThroughSlot),
            ReconciledThroughSlot = ToDatabaseSlot(initialThroughSlot),
            Status = "Healthy",
            UpdatedTime = now
        };
        context.IngestionCheckpoints.Add(checkpoint);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ToSnapshot(checkpoint);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(checkpoint).State = EntityState.Detached;
            var existing = await FindCheckpointAsync(key, cancellationToken);
            if (existing is null)
            {
                throw new InvalidOperationException(
                    "Checkpoint conflicted but could not be reloaded.",
                    exception);
            }

            return ToSnapshot(existing);
        }
    }

    public async Task RecordRealtimeObservationAsync(
        IngestionCheckpointKey key,
        ulong slot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var initial = slot == 0 ? 0 : slot - 1;
        var checkpoint = await GetOrCreateEntityAsync(
            key,
            initial,
            now,
            cancellationToken);
        await UpsertSlotStateAsync(
            checkpoint.Id,
            slot,
            persisted: false,
            gapReason: null,
            now,
            cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE ingestion_checkpoints
            SET observed_through_slot = GREATEST(
                    observed_through_slot,
                    {ToDatabaseSlot(slot)}),
                status = CASE
                    WHEN status = 'Gapped' THEN status
                    ELSE 'CatchingUp'
                END,
                updated_time = {now}
            WHERE id = {checkpoint.Id}
            """, cancellationToken);
    }

    public async Task RecordBackfillCoverageAsync(
        Guid checkpointId,
        ulong fromExclusive,
        ulong toInclusive,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (toInclusive <= fromExclusive)
        {
            return;
        }

        var checkpoint = await context.IngestionCheckpoints
            .SingleAsync(value => value.Id == checkpointId, cancellationToken);
        for (var slot = fromExclusive + 1; slot <= toInclusive; slot++)
        {
            await UpsertSlotStateAsync(
                checkpointId,
                slot,
                persisted: true,
                gapReason: null,
                now,
                cancellationToken);
            if (slot == ulong.MaxValue)
            {
                break;
            }
        }

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE ingestion_checkpoints
            SET observed_through_slot = GREATEST(
                    observed_through_slot,
                    {ToDatabaseSlot(toInclusive)}),
                updated_time = {now}
            WHERE id = {checkpointId}
            """, cancellationToken);
    }

    public async Task MarkGapAsync(
        Guid checkpointId,
        ulong slot,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        await UpsertSlotStateAsync(
            checkpointId,
            slot,
            persisted: false,
            gapReason: reason,
            now,
            cancellationToken);
        var checkpoint = await context.IngestionCheckpoints
            .SingleAsync(value => value.Id == checkpointId, cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE ingestion_checkpoints
            SET status = 'Gapped',
                updated_time = {now}
            WHERE id = {checkpoint.Id}
            """, cancellationToken);
    }

    public Task ResolveGapAsync(
        Guid checkpointId,
        ulong slot,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var truncatedReason = reason[..Math.Min(reason.Length, 1_000)];
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE ingestion_slot_states
            SET has_gap = FALSE,
                gap_reason = NULL,
                updated_time = {now}
            WHERE checkpoint_id = {checkpointId}
              AND slot = {ToDatabaseSlot(slot)}
              AND gap_reason = {truncatedReason}
            """, cancellationToken);
    }

    public async Task PromoteSignatureToFinalizedAsync(
        string signature,
        DateTimeOffset finalizedAt,
        CancellationToken cancellationToken)
    {
        var events = await context.RawBlockchainEvents
            .Where(value =>
                value.TransactionSignature == signature &&
                value.EventType == "SolanaTransaction" &&
                value.CanonicalStatus != CanonicalStatus.Reverted)
            .ToListAsync(cancellationToken);
        foreach (var value in events)
        {
            var needsFinalizedReplay =
                value.CanonicalStatus != CanonicalStatus.Finalized &&
                value.ProcessingStatus == ProcessingStatus.Completed;
            value.CanonicalStatus = CanonicalStatus.Finalized;
            value.CommitmentLevel = "finalized";
            value.FinalizedTime ??= finalizedAt;
            value.FinalityUpdatedTime = finalizedAt;
            value.UpdatedTime = finalizedAt;
            if (needsFinalizedReplay)
            {
                value.ProcessingStatus = ProcessingStatus.Pending;
                value.RetryCount = 0;
                value.FirstFailureTime = null;
                value.LastFailureTime = null;
                value.LastError = null;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IngestionCheckpointSnapshot> RefreshAndAdvanceAsync(
        Guid checkpointId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var checkpoint = await context.IngestionCheckpoints
            .SingleAsync(value => value.Id == checkpointId, cancellationToken);
        var states = await context.IngestionSlotStates
            .Where(value =>
                value.CheckpointId == checkpointId &&
                value.Slot > checkpoint.ReconciledThroughSlot)
            .OrderBy(value => value.Slot)
            .ToListAsync(cancellationToken);
        if (states.Count == 0)
        {
            return ToSnapshot(checkpoint);
        }

        var minimumSlot = states[0].Slot;
        var maximumSlot = states[^1].Slot;
        var rawEvents = await context.RawBlockchainEvents
            .AsNoTracking()
            .Where(value =>
                value.ProgramId == checkpoint.SubscriptionType &&
                value.EventType == "SolanaTransaction" &&
                value.Slot >= minimumSlot &&
                value.Slot <= maximumSlot)
            .Select(value => new
            {
                value.Slot,
                value.ProcessingStatus,
                value.CanonicalStatus,
                value.TransactionSignature
            })
            .ToListAsync(cancellationToken);
        var bySlot = rawEvents
            .GroupBy(value => value.Slot)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var state in states)
        {
            bySlot.TryGetValue(state.Slot, out var slotEvents);
            slotEvents ??= [];
            state.Processed = state.Persisted &&
                              slotEvents.All(value =>
                                  value.ProcessingStatus == ProcessingStatus.Completed);
            state.Finalized = state.Processed &&
                              slotEvents.All(value =>
                                  value.CanonicalStatus == CanonicalStatus.Finalized);
            state.Reconciled = state.Finalized && !state.HasGap;
            state.UpdatedTime = now;
        }

        var current = ToWatermarks(checkpoint);
        var advanced = CheckpointAdvancer.AdvanceContinuous(
            current,
            states.Select(ToCompletion));
        var status = states.Any(value => value.HasGap)
            ? "Gapped"
            : advanced.ReconciledThroughSlot == advanced.ObservedThroughSlot
                ? "Healthy"
                : "CatchingUp";
        var lastCompletedSignature = rawEvents
            .Where(value => value.Slot <= ToDatabaseSlot(
                advanced.ReconciledThroughSlot))
            .OrderByDescending(value => value.Slot)
            .Select(value => value.TransactionSignature)
            .FirstOrDefault();
        await context.SaveChangesAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE ingestion_checkpoints
            SET observed_through_slot = GREATEST(
                    observed_through_slot,
                    {ToDatabaseSlot(advanced.ObservedThroughSlot)}),
                persisted_through_slot = GREATEST(
                    persisted_through_slot,
                    {ToDatabaseSlot(advanced.PersistedThroughSlot)}),
                processed_through_slot = GREATEST(
                    processed_through_slot,
                    {ToDatabaseSlot(advanced.ProcessedThroughSlot)}),
                finalized_through_slot = GREATEST(
                    finalized_through_slot,
                    {ToDatabaseSlot(advanced.FinalizedThroughSlot)}),
                reconciled_through_slot = GREATEST(
                    reconciled_through_slot,
                    {ToDatabaseSlot(advanced.ReconciledThroughSlot)}),
                status = CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM ingestion_slot_states
                        WHERE checkpoint_id = {checkpointId}
                          AND has_gap
                    ) THEN 'Gapped'
                    ELSE {status}
                END,
                last_completed_signature = COALESCE(
                    {lastCompletedSignature},
                    last_completed_signature),
                updated_time = {now}
            WHERE id = {checkpointId}
            """, cancellationToken);
        context.Entry(checkpoint).State = EntityState.Detached;
        var refreshed = await context.IngestionCheckpoints
            .AsNoTracking()
            .SingleAsync(value => value.Id == checkpointId, cancellationToken);
        return ToSnapshot(refreshed);
    }

    public async Task<IReadOnlyList<IngestionCheckpointSnapshot>> ListCheckpointsAsync(
        CancellationToken cancellationToken)
    {
        var values = await context.IngestionCheckpoints
            .AsNoTracking()
            .OrderBy(value => value.Source)
            .ThenBy(value => value.SubscriptionType)
            .ToArrayAsync(cancellationToken);
        return values.Select(ToSnapshot).ToArray();
    }

    public async Task<IReadOnlyList<IngestionGap>> ListGapsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 1_000);
        var values = await (
                from state in context.IngestionSlotStates.AsNoTracking()
                join checkpoint in context.IngestionCheckpoints.AsNoTracking()
                    on state.CheckpointId equals checkpoint.Id
                where state.HasGap
                orderby state.UpdatedTime descending
                select new
                {
                    state.CheckpointId,
                    checkpoint.SubscriptionType,
                    state.Slot,
                    state.GapReason,
                    state.UpdatedTime
                })
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return values.Select(value => new IngestionGap(
                value.CheckpointId,
                value.SubscriptionType,
                checked((ulong)value.Slot),
                value.GapReason ?? "Unknown gap.",
                value.UpdatedTime))
            .ToArray();
    }

    private async Task<IngestionCheckpointEntity> GetOrCreateEntityAsync(
        IngestionCheckpointKey key,
        ulong initialThroughSlot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await FindCheckpointAsync(key, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        await GetOrCreateAsync(key, initialThroughSlot, now, cancellationToken);
        return await FindCheckpointAsync(key, cancellationToken)
               ?? throw new InvalidOperationException("Checkpoint creation failed.");
    }

    private Task<IngestionCheckpointEntity?> FindCheckpointAsync(
        IngestionCheckpointKey key,
        CancellationToken cancellationToken) =>
        context.IngestionCheckpoints.SingleOrDefaultAsync(
            value =>
                value.Chain == key.Chain &&
                value.Network == key.Network &&
                value.Source == key.Source &&
                value.SubscriptionType == key.SubscriptionType,
            cancellationToken);

    private Task<int> UpsertSlotStateAsync(
        Guid checkpointId,
        ulong slot,
        bool persisted,
        string? gapReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var databaseSlot = ToDatabaseSlot(slot);
        var id = Guid.NewGuid();
        var truncatedReason = gapReason is null
            ? null
            : gapReason[..Math.Min(gapReason.Length, 1_000)];
        var hasGap = truncatedReason is not null;
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ingestion_slot_states (
                id,
                checkpoint_id,
                slot,
                observed,
                persisted,
                processed,
                finalized,
                reconciled,
                has_gap,
                gap_reason,
                updated_time)
            VALUES (
                {id},
                {checkpointId},
                {databaseSlot},
                TRUE,
                {persisted},
                FALSE,
                FALSE,
                FALSE,
                {hasGap},
                {truncatedReason},
                {now})
            ON CONFLICT (checkpoint_id, slot) DO UPDATE
            SET observed = TRUE,
                persisted = ingestion_slot_states.persisted OR EXCLUDED.persisted,
                reconciled = CASE
                    WHEN EXCLUDED.has_gap THEN FALSE
                    ELSE ingestion_slot_states.reconciled
                END,
                has_gap = ingestion_slot_states.has_gap OR EXCLUDED.has_gap,
                gap_reason = COALESCE(
                    EXCLUDED.gap_reason,
                    ingestion_slot_states.gap_reason),
                updated_time = EXCLUDED.updated_time
            """, cancellationToken);
    }

    private static IngestionCheckpointSnapshot ToSnapshot(
        IngestionCheckpointEntity value) => new(
        value.Id,
        new IngestionCheckpointKey(
            value.Chain,
            value.Network,
            value.Source,
            value.SubscriptionType),
        ToWatermarks(value),
        value.Status,
        value.UpdatedTime);

    private static IngestionWatermarks ToWatermarks(
        IngestionCheckpointEntity value) => new(
        checked((ulong)value.ObservedThroughSlot),
        checked((ulong)value.PersistedThroughSlot),
        checked((ulong)value.ProcessedThroughSlot),
        checked((ulong)value.FinalizedThroughSlot),
        checked((ulong)value.ReconciledThroughSlot));

    private static SlotCompletion ToCompletion(
        IngestionSlotStateEntity value) => new(
        checked((ulong)value.Slot),
        value.Observed,
        value.Persisted,
        value.Processed,
        value.Finalized,
        value.Reconciled,
        value.HasGap);

    private static long ToDatabaseSlot(ulong value) => checked((long)value);
}
