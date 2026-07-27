using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Domain.Ingestion;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresRawEventStore(
    CryptoIntelligenceDbContext context)
    : IRawEventStore
{
    public async Task<PersistedRawEvent> PersistAsync(
        RawBlockchainEventInput input,
        CancellationToken cancellationToken)
    {
        var eventId = input.Identity.EventId;
        var existing = await context.RawBlockchainEvents
            .AsNoTracking()
            .Where(value => value.EventId == eventId)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing.HasValue)
        {
            return new PersistedRawEvent(existing.Value, eventId, Inserted: false);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new RawBlockchainEventEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Chain = input.Identity.Chain,
            Network = input.Identity.Network,
            Slot = checked((long)input.Slot),
            BlockHash = input.BlockHash,
            TransactionSignature = input.Identity.TransactionSignature,
            InstructionIndex = input.Identity.InstructionIndex,
            InnerInstructionIndex = input.Identity.InnerInstructionIndex,
            ProgramId = input.ProgramId,
            EventType = input.Identity.EventType,
            EventOrdinal = input.Identity.EventOrdinal,
            EventTime = input.EventTime,
            ObservedTime = input.ObservedTime,
            CommitmentLevel = input.CommitmentLevel,
            CanonicalStatus = input.CanonicalStatus,
            FinalityUpdatedTime = input.ObservedTime,
            Source = input.Source,
            RawPayload = input.RawPayload,
            SchemaVersion = input.Identity.SchemaVersion,
            ProcessingStatus = ProcessingStatus.Pending,
            CorrelationId = input.CorrelationId,
            CreatedTime = now,
            UpdatedTime = now
        };

        context.RawBlockchainEvents.Add(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return new PersistedRawEvent(entity.Id, eventId, Inserted: true);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(entity).State = EntityState.Detached;
            var existingId = await context.RawBlockchainEvents
                .AsNoTracking()
                .Where(value => value.EventId == eventId)
                .Select(value => value.Id)
                .SingleAsync(cancellationToken);
            return new PersistedRawEvent(existingId, eventId, Inserted: false);
        }
    }

    public async Task<IReadOnlyList<LeasedRawEvent>> LeasePendingAsync(
        string workerId,
        int batchSize,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        var events = await context.RawBlockchainEvents
            .FromSqlInterpolated($"""
                SELECT *
                FROM raw_blockchain_events
                WHERE processing_status IN ('Pending', 'RetryableFailure')
                   OR (processing_status = 'Processing' AND lease_until <= {now})
                ORDER BY observed_time, id
                FOR UPDATE SKIP LOCKED
                LIMIT {batchSize}
                """)
            .ToListAsync(cancellationToken);

        foreach (var entity in events)
        {
            entity.ProcessingStatus = ProcessingStatus.Processing;
            entity.LeaseOwner = workerId;
            entity.LeaseUntil = now.Add(leaseDuration);
            entity.UpdatedTime = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return events.Select(ToLeasedEvent).ToArray();
    }

    public Task CompleteAsync(
        Guid id,
        string workerId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken) =>
        UpdateLeaseAsync(
            id,
            workerId,
            ProcessingStatus.Completed,
            error: null,
            maximumRetries: null,
            completedAt,
            cancellationToken);

    public Task FailAsync(
        Guid id,
        string workerId,
        string error,
        int maximumRetries,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken) =>
        UpdateLeaseAsync(
            id,
            workerId,
            ProcessingStatus.RetryableFailure,
            error,
            maximumRetries,
            failedAt,
            cancellationToken);

    private async Task UpdateLeaseAsync(
        Guid id,
        string workerId,
        ProcessingStatus requestedStatus,
        string? error,
        int? maximumRetries,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var entity = await context.RawBlockchainEvents
            .SingleAsync(value => value.Id == id, cancellationToken);
        if (entity.ProcessingStatus != ProcessingStatus.Processing ||
            !string.Equals(entity.LeaseOwner, workerId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Raw event '{id}' is not leased by worker '{workerId}'.");
        }

        if (requestedStatus == ProcessingStatus.Completed)
        {
            entity.ProcessingStatus = ProcessingStatus.Completed;
        }
        else
        {
            entity.RetryCount++;
            entity.FirstFailureTime ??= timestamp;
            entity.LastFailureTime = timestamp;
            entity.LastError = error is null
                ? null
                : error[..Math.Min(error.Length, 2_000)];
            entity.ProcessingStatus = entity.RetryCount >= maximumRetries
                ? ProcessingStatus.DeadLetter
                : ProcessingStatus.RetryableFailure;
        }

        entity.LeaseOwner = null;
        entity.LeaseUntil = null;
        entity.UpdatedTime = timestamp;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static LeasedRawEvent ToLeasedEvent(RawBlockchainEventEntity entity)
    {
        var identity = new RawEventIdentity(
            entity.Chain,
            entity.Network,
            entity.TransactionSignature,
            entity.InstructionIndex,
            entity.InnerInstructionIndex,
            entity.EventType,
            entity.EventOrdinal,
            entity.SchemaVersion);
        var input = new RawBlockchainEventInput(
            identity,
            checked((ulong)entity.Slot),
            entity.BlockHash,
            entity.ProgramId,
            entity.EventTime,
            entity.ObservedTime,
            entity.CommitmentLevel,
            entity.CanonicalStatus,
            entity.Source,
            entity.RawPayload,
            entity.CorrelationId);
        return new LeasedRawEvent(entity.Id, entity.EventId, input, entity.RetryCount);
    }
}
