using System.Text.Json;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Application.Tests;

public sealed class SolanaAdapterHandlerTests
{
    [Fact]
    public async Task Handler_parses_persisted_raw_transaction_and_appends_events()
    {
        var adapter = new StubAdapter();
        var store = new RecordingNormalizedStore();
        var handler = new SolanaAdapterRawEventHandler(adapter, store);
        var rawEvent = CreateRawEvent(slot: 123);

        await handler.HandleAsync(rawEvent, CancellationToken.None);

        Assert.Equal(rawEvent.Id, store.RawEventId);
        Assert.Single(store.Events);
        Assert.Equal("PoolCreated", store.Events[0].DomainEventType);
    }

    [Fact]
    public async Task Handler_rejects_adapter_slot_mismatch()
    {
        var handler = new SolanaAdapterRawEventHandler(
            new StubAdapter(),
            new RecordingNormalizedStore());

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.HandleAsync(CreateRawEvent(slot: 999), CancellationToken.None));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discovery_handler_durably_creates_transaction_event()
    {
        var transaction = new SolanaTransactionPayload(
            "signature",
            123,
            DateTimeOffset.UnixEpoch,
            "confirmed",
            "primary",
            """{"result":{"slot":123}}""");
        var store = new RecordingRawStore();
        var handler = new SolanaDiscoveryRawEventHandler(
            new StubTransactionSource(transaction),
            store);

        await handler.HandleAsync(
            CreateDiscoveryEvent(),
            CancellationToken.None);

        Assert.NotNull(store.Input);
        Assert.Equal("SolanaTransaction", store.Input.Identity.EventType);
        Assert.Equal(transaction.Json, store.Input.RawPayload);
    }

    [Fact]
    public async Task Discovery_handler_keeps_event_retryable_when_rpc_data_is_unavailable()
    {
        var handler = new SolanaDiscoveryRawEventHandler(
            new StubTransactionSource(null),
            new RecordingRawStore());

        await Assert.ThrowsAsync<SolanaDataUnavailableException>(() =>
            handler.HandleAsync(CreateDiscoveryEvent(), CancellationToken.None));
    }

    private static LeasedRawEvent CreateRawEvent(ulong slot)
    {
        var identity = new RawEventIdentity(
            "Solana",
            "mainnet-beta",
            "signature",
            -1,
            null,
            "SolanaTransaction",
            0,
            "solana-transaction-v1");
        var input = new RawBlockchainEventInput(
            identity,
            slot,
            null,
            "program",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "confirmed",
            CanonicalStatus.Confirmed,
            "rpc",
            "{}",
            null);
        return new LeasedRawEvent(Guid.NewGuid(), identity.EventId, input, 0);
    }

    private static LeasedRawEvent CreateDiscoveryEvent()
    {
        var notification = new SolanaSignatureNotification(
            "program",
            "signature",
            123,
            false,
            DateTimeOffset.UnixEpoch);
        var identity = new RawEventIdentity(
            "Solana",
            "mainnet-beta",
            notification.Signature,
            -1,
            null,
            "SolanaSignatureDiscovered",
            0,
            "solana-discovery-v1");
        var input = new RawBlockchainEventInput(
            identity,
            notification.Slot,
            null,
            notification.ProgramId,
            notification.ObservedAt,
            notification.ObservedAt,
            "confirmed",
            CanonicalStatus.Observed,
            "primary",
            JsonSerializer.Serialize(notification),
            notification.Signature);
        return new LeasedRawEvent(Guid.NewGuid(), identity.EventId, input, 0);
    }

    private sealed class StubAdapter : ISolanaTransactionAdapter
    {
        public string ParserVersion => "parser-v1";

        public IReadOnlySet<string> ProgramIds { get; } = new HashSet<string>
        {
            "program"
        };

        public AdapterParseResult Parse(string transactionJson) => new(
            123,
            false,
            ParserVersion,
            [
                new ParsedAdapterEvent(
                    "program",
                    "PoolCreateEvent",
                    0,
                    null,
                    0,
                    "PoolCreated",
                    "ProgramData",
                    "fingerprint")
            ]);
    }

    private sealed class RecordingNormalizedStore : INormalizedEventStore
    {
        public Guid RawEventId { get; private set; }

        public IReadOnlyList<ParsedAdapterEvent> Events { get; private set; } = [];

        public Task AppendAsync(
            Guid rawEventId,
            DateTimeOffset eventTime,
            string parserVersion,
            IReadOnlyList<ParsedAdapterEvent> events,
            CancellationToken cancellationToken)
        {
            RawEventId = rawEventId;
            Events = events;
            return Task.CompletedTask;
        }
    }

    private sealed class StubTransactionSource(SolanaTransactionPayload? result)
        : ISolanaTransactionSource
    {
        public Task<SolanaTransactionPayload?> FetchAsync(
            string signature,
            string commitment,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class RecordingRawStore : IRawEventStore
    {
        public RawBlockchainEventInput? Input { get; private set; }

        public Task<PersistedRawEvent> PersistAsync(
            RawBlockchainEventInput input,
            CancellationToken cancellationToken)
        {
            Input = input;
            return Task.FromResult(
                new PersistedRawEvent(Guid.NewGuid(), input.Identity.EventId, true));
        }

        public Task<IReadOnlyList<LeasedRawEvent>> LeasePendingAsync(
            string workerId,
            int batchSize,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CompleteAsync(
            Guid id,
            string workerId,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task FailAsync(
            Guid id,
            string workerId,
            string error,
            int maximumRetries,
            DateTimeOffset failedAt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
