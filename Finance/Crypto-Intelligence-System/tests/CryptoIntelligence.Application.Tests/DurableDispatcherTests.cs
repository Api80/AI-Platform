using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Application.Tests;

public sealed class DurableDispatcherTests
{
    [Fact]
    public async Task Dispatcher_completes_a_successful_event()
    {
        var store = new RecordingStore([CreateEvent()]);
        var dispatcher = new DurableRawEventDispatcher(store, [new SuccessfulHandler()]);

        var count = await dispatcher.DispatchBatchAsync(
            "worker-a",
            10,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1),
            3,
            CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Single(store.Completed);
        Assert.Empty(store.Failed);
    }

    [Fact]
    public async Task Dispatcher_records_failure_without_losing_event()
    {
        var store = new RecordingStore([CreateEvent()]);
        var dispatcher = new DurableRawEventDispatcher(store, [new FailingHandler()]);

        await dispatcher.DispatchBatchAsync(
            "worker-a",
            10,
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(1),
            3,
            CancellationToken.None);

        Assert.Empty(store.Completed);
        Assert.Single(store.Failed);
        Assert.Contains("parser failed", store.Failed.Single().Error, StringComparison.Ordinal);
    }

    private static LeasedRawEvent CreateEvent()
    {
        var identity = new RawEventIdentity(
            "Solana",
            "mainnet-beta",
            "signature",
            0,
            null,
            "PoolCreated",
            0,
            "v1");
        var input = new RawBlockchainEventInput(
            identity,
            1,
            null,
            "program",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "confirmed",
            CanonicalStatus.Confirmed,
            "fixture",
            "{}",
            null);
        return new LeasedRawEvent(Guid.NewGuid(), identity.EventId, input, 0);
    }

    private sealed class SuccessfulHandler : IRawEventHandler
    {
        public Task HandleAsync(
            LeasedRawEvent rawEvent,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingHandler : IRawEventHandler
    {
        public Task HandleAsync(
            LeasedRawEvent rawEvent,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("parser failed");
    }

    private sealed class RecordingStore(IReadOnlyList<LeasedRawEvent> events)
        : IRawEventStore
    {
        public List<Guid> Completed { get; } = [];
        public List<(Guid Id, string Error)> Failed { get; } = [];

        public Task<PersistedRawEvent> PersistAsync(
            RawBlockchainEventInput input,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<LeasedRawEvent>> LeasePendingAsync(
            string workerId,
            int batchSize,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            Task.FromResult(events);

        public Task CompleteAsync(
            Guid id,
            string workerId,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken)
        {
            Completed.Add(id);
            return Task.CompletedTask;
        }

        public Task FailAsync(
            Guid id,
            string workerId,
            string error,
            int maximumRetries,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken)
        {
            Failed.Add((id, error));
            return Task.CompletedTask;
        }
    }
}
