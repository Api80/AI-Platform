using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Application.Ingestion;

public sealed record RawBlockchainEventInput(
    RawEventIdentity Identity,
    ulong Slot,
    string? BlockHash,
    string ProgramId,
    DateTimeOffset EventTime,
    DateTimeOffset ObservedTime,
    string CommitmentLevel,
    CanonicalStatus CanonicalStatus,
    string Source,
    string RawPayload,
    string? CorrelationId);

public sealed record PersistedRawEvent(
    Guid Id,
    string EventId,
    bool Inserted);

public sealed record LeasedRawEvent(
    Guid Id,
    string EventId,
    RawBlockchainEventInput Event,
    int RetryCount);

public interface IRawEventStore
{
    Task<PersistedRawEvent> PersistAsync(
        RawBlockchainEventInput input,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LeasedRawEvent>> LeasePendingAsync(
        string workerId,
        int batchSize,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid id,
        string workerId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);

    Task FailAsync(
        Guid id,
        string workerId,
        string error,
        int maximumRetries,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken);
}

public interface IRawEventHandler
{
    Task HandleAsync(
        LeasedRawEvent rawEvent,
        CancellationToken cancellationToken);
}

public sealed class DurableRawEventDispatcher(
    IRawEventStore store,
    IEnumerable<IRawEventHandler> handlers)
{
    public async Task<int> DispatchBatchAsync(
        string workerId,
        int batchSize,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        int maximumRetries,
        CancellationToken cancellationToken)
    {
        var events = await store.LeasePendingAsync(
            workerId,
            batchSize,
            now,
            leaseDuration,
            cancellationToken);

        foreach (var rawEvent in events)
        {
            try
            {
                foreach (var handler in handlers)
                {
                    await handler.HandleAsync(rawEvent, cancellationToken);
                }

                await store.CompleteAsync(
                    rawEvent.Id,
                    workerId,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await store.FailAsync(
                    rawEvent.Id,
                    workerId,
                    exception.Message,
                    maximumRetries,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }
        }

        return events.Count;
    }
}
