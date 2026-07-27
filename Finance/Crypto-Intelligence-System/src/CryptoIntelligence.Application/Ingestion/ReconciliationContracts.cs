using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Application.Ingestion;

public sealed record IngestionCheckpointKey(
    string Chain,
    string Network,
    string Source,
    string SubscriptionType);

public sealed record IngestionCheckpointSnapshot(
    Guid Id,
    IngestionCheckpointKey Key,
    IngestionWatermarks Watermarks,
    string Status,
    DateTimeOffset UpdatedTime);

public sealed record SolanaBackfillSignature(
    string Signature,
    ulong Slot,
    bool Failed,
    DateTimeOffset? BlockTime);

public sealed record SolanaBackfillBatch(
    ulong FromExclusive,
    ulong ToInclusive,
    bool Complete,
    IReadOnlyList<SolanaBackfillSignature> Signatures);

public sealed record IngestionGap(
    Guid CheckpointId,
    string SubscriptionType,
    ulong Slot,
    string Reason,
    DateTimeOffset UpdatedTime);

public sealed record StorageTableCapacity(
    string TableName,
    long EstimatedRows,
    long DataBytes,
    long IndexBytes,
    long TotalBytes,
    bool IsPartitioned);

public sealed record IngestionCapacityReport(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<StorageTableCapacity> Tables,
    long EventsLast24Hours,
    long RawBytesLast24Hours,
    long SwapsLast24Hours,
    long MarketSnapshotsLast24Hours,
    DateTimeOffset? OldestRawEventTime,
    DateTimeOffset? NewestRawEventTime)
{
    public long TotalBytes => Tables.Sum(value => value.TotalBytes);
}

public interface ISolanaBackfillSource
{
    Task<ulong> GetFinalizedSlotAsync(CancellationToken cancellationToken);

    Task<SolanaBackfillBatch> ListFinalizedSignaturesAsync(
        string programId,
        ulong fromExclusive,
        ulong toInclusive,
        int maximumSignatures,
        CancellationToken cancellationToken);
}

public interface IIngestionReconciliationStore
{
    Task<IngestionCheckpointSnapshot> GetOrCreateAsync(
        IngestionCheckpointKey key,
        ulong initialThroughSlot,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task RecordRealtimeObservationAsync(
        IngestionCheckpointKey key,
        ulong slot,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task RecordBackfillCoverageAsync(
        Guid checkpointId,
        ulong fromExclusive,
        ulong toInclusive,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkGapAsync(
        Guid checkpointId,
        ulong slot,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task ResolveGapAsync(
        Guid checkpointId,
        ulong slot,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task PromoteSignatureToFinalizedAsync(
        string signature,
        DateTimeOffset finalizedAt,
        CancellationToken cancellationToken);

    Task<IngestionCheckpointSnapshot> RefreshAndAdvanceAsync(
        Guid checkpointId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IngestionCheckpointSnapshot>> ListCheckpointsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IngestionGap>> ListGapsAsync(
        int limit,
        CancellationToken cancellationToken);
}

public interface IIngestionOperationsQuery
{
    Task<IngestionCapacityReport> GetCapacityReportAsync(
        CancellationToken cancellationToken);
}

public sealed record ReconciliationCycleResult(
    string ProgramId,
    ulong FromExclusive,
    ulong ToInclusive,
    int SignatureCount,
    int PersistedTransactionCount,
    int GapCount,
    IngestionWatermarks Watermarks);

public sealed class SolanaBackfillReconciliationService(
    ISolanaBackfillSource backfill,
    ISolanaTransactionSource transactions,
    IRawEventStore rawEvents,
    IIngestionReconciliationStore reconciliationStore,
    string sourceName,
    int maximumSlotsPerCycle,
    int maximumSignaturesPerCycle)
{
    public async Task<ReconciliationCycleResult> RunCycleAsync(
        string programId,
        ulong initialThroughSlot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programId);
        if (maximumSlotsPerCycle <= 0 || maximumSignaturesPerCycle <= 0)
        {
            throw new InvalidOperationException(
                "Backfill slot and signature limits must be positive.");
        }

        var key = new IngestionCheckpointKey(
            "Solana",
            "mainnet-beta",
            sourceName,
            programId);
        var checkpoint = await reconciliationStore.GetOrCreateAsync(
            key,
            initialThroughSlot,
            now,
            cancellationToken);
        var from = checkpoint.Watermarks.ReconciledThroughSlot;
        var finalizedHead = await backfill.GetFinalizedSlotAsync(cancellationToken);
        var to = Math.Min(
            finalizedHead,
            AddSaturating(from, checked((ulong)maximumSlotsPerCycle)));
        if (to <= from)
        {
            var refreshed = await reconciliationStore.RefreshAndAdvanceAsync(
                checkpoint.Id,
                now,
                cancellationToken);
            return new ReconciliationCycleResult(
                programId,
                from,
                to,
                0,
                0,
                0,
                refreshed.Watermarks);
        }

        var batch = await backfill.ListFinalizedSignaturesAsync(
            programId,
            from,
            to,
            maximumSignaturesPerCycle,
            cancellationToken);

        var persisted = 0;
        var gaps = 0;
        foreach (var signature in batch.Signatures)
        {
            if (signature.Failed)
            {
                continue;
            }

            var transaction = await transactions.FetchAsync(
                signature.Signature,
                "finalized",
                cancellationToken);
            if (transaction is null)
            {
                gaps++;
                await reconciliationStore.MarkGapAsync(
                    checkpoint.Id,
                    signature.Slot,
                    MissingTransactionReason(signature.Signature),
                    now,
                    cancellationToken);
                continue;
            }

            var input = new RawBlockchainEventInput(
                new RawEventIdentity(
                    "Solana",
                    "mainnet-beta",
                    signature.Signature,
                    -1,
                    null,
                    "SolanaTransaction",
                    0,
                    "solana-transaction-v1"),
                transaction.Slot,
                null,
                programId,
                transaction.EventTime,
                now,
                "finalized",
                CanonicalStatus.Finalized,
                transaction.Source,
                transaction.Json,
                signature.Signature);
            var result = await rawEvents.PersistAsync(input, cancellationToken);
            if (result.Inserted)
            {
                persisted++;
            }

            await reconciliationStore.PromoteSignatureToFinalizedAsync(
                signature.Signature,
                now,
                cancellationToken);
            await reconciliationStore.ResolveGapAsync(
                checkpoint.Id,
                signature.Slot,
                MissingTransactionReason(signature.Signature),
                now,
                cancellationToken);
        }

        await reconciliationStore.RecordBackfillCoverageAsync(
            checkpoint.Id,
            from,
            to,
            now,
            cancellationToken);
        const string incompleteRangeReason =
            "Backfill signature limit was reached before the slot range was exhausted.";
        if (!batch.Complete)
        {
            gaps++;
            await reconciliationStore.MarkGapAsync(
                checkpoint.Id,
                to,
                incompleteRangeReason,
                now,
                cancellationToken);
        }
        else
        {
            await reconciliationStore.ResolveGapAsync(
                checkpoint.Id,
                to,
                incompleteRangeReason,
                now,
                cancellationToken);
        }

        var updated = await reconciliationStore.RefreshAndAdvanceAsync(
            checkpoint.Id,
            now,
            cancellationToken);
        return new ReconciliationCycleResult(
            programId,
            from,
            to,
            batch.Signatures.Count,
            persisted,
            gaps,
            updated.Watermarks);
    }

    private static ulong AddSaturating(ulong left, ulong right) =>
        ulong.MaxValue - left < right ? ulong.MaxValue : left + right;

    private static string MissingTransactionReason(string signature) =>
        $"Finalized transaction '{signature}' is unavailable.";
}
