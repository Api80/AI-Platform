using System.Text.Json;
using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Application.Ingestion;

public sealed record SolanaSignatureNotification(
    string ProgramId,
    string Signature,
    ulong Slot,
    bool Failed,
    DateTimeOffset ObservedAt);

public sealed record SolanaTransactionPayload(
    string Signature,
    ulong Slot,
    DateTimeOffset EventTime,
    string Commitment,
    string Source,
    string Json);

public interface ISolanaDiscoverySource
{
    IAsyncEnumerable<SolanaSignatureNotification> DiscoverAsync(
        CancellationToken cancellationToken);
}

public interface ISolanaTransactionSource
{
    Task<SolanaTransactionPayload?> FetchAsync(
        string signature,
        string commitment,
        CancellationToken cancellationToken);
}

public interface IDiscoveryConnectionObserver
{
    ValueTask ConnectedAsync(
        string source,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);

    ValueTask DisconnectedAsync(
        string source,
        string reason,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}

public sealed record ParsedAdapterEvent(
    string ProgramId,
    string Name,
    int InstructionIndex,
    int? InnerInstructionIndex,
    int EventOrdinal,
    string DomainEventType,
    string Source,
    string PayloadFingerprint,
    IReadOnlyDictionary<string, string>? Attributes = null);

public sealed record AdapterParseResult(
    ulong Slot,
    bool Failed,
    string ParserVersion,
    IReadOnlyList<ParsedAdapterEvent> Events);

public interface ISolanaTransactionAdapter
{
    string ParserVersion { get; }

    IReadOnlySet<string> ProgramIds { get; }

    AdapterParseResult Parse(string transactionJson);
}

public interface INormalizedEventStore
{
    Task AppendAsync(
        Guid rawEventId,
        DateTimeOffset eventTime,
        string parserVersion,
        IReadOnlyList<ParsedAdapterEvent> events,
        CancellationToken cancellationToken);
}

public sealed class SolanaAdapterRawEventHandler(
    ISolanaTransactionAdapter adapter,
    INormalizedEventStore store,
    IEnumerable<CryptoIntelligence.Application.Radar.IProjectionEventHandler> projectionHandlers)
    : IRawEventHandler
{
    public SolanaAdapterRawEventHandler(
        ISolanaTransactionAdapter adapter,
        INormalizedEventStore store)
        : this(adapter, store, [])
    {
    }

    public async Task HandleAsync(
        LeasedRawEvent rawEvent,
        CancellationToken cancellationToken)
    {
        if (rawEvent.Event.Identity.EventType != "SolanaTransaction")
        {
            return;
        }

        var parsed = adapter.Parse(rawEvent.Event.RawPayload);
        if (parsed.Slot != rawEvent.Event.Slot)
        {
            throw new InvalidDataException(
                $"Adapter slot {parsed.Slot} does not match raw event slot {rawEvent.Event.Slot}.");
        }

        await store.AppendAsync(
            rawEvent.Id,
            rawEvent.Event.EventTime,
            parsed.ParserVersion,
            parsed.Events,
            cancellationToken);
        foreach (var parsedEvent in parsed.Events)
        {
            var projectionEvent = new CryptoIntelligence.Application.Radar.ProjectionEvent(
                rawEvent.Id,
                rawEvent.Event.Slot,
                rawEvent.Event.EventTime,
                rawEvent.Event.ObservedTime,
                parsedEvent,
                rawEvent.Event.CanonicalStatus);
            foreach (var handler in projectionHandlers)
            {
                await handler.HandleAsync(projectionEvent, cancellationToken);
            }
        }
    }
}

public sealed class SolanaDiscoveryRawEventHandler(
    ISolanaTransactionSource transactions,
    IRawEventStore rawEventStore)
    : IRawEventHandler
{
    public async Task HandleAsync(
        LeasedRawEvent rawEvent,
        CancellationToken cancellationToken)
    {
        if (rawEvent.Event.Identity.EventType != "SolanaSignatureDiscovered")
        {
            return;
        }

        var notification =
            JsonSerializer.Deserialize<SolanaSignatureNotification>(
                rawEvent.Event.RawPayload)
            ?? throw new InvalidDataException("Discovery payload is invalid.");
        var transaction = await transactions.FetchAsync(
            notification.Signature,
            rawEvent.Event.CommitmentLevel,
            cancellationToken);
        if (transaction is null)
        {
            throw new SolanaDataUnavailableException(
                notification.Signature,
                notification.Slot);
        }

        var identity = new RawEventIdentity(
            "Solana",
            "mainnet-beta",
            notification.Signature,
            -1,
            null,
            "SolanaTransaction",
            0,
            "solana-transaction-v1");
        var input = new RawBlockchainEventInput(
            identity,
            transaction.Slot,
            null,
            notification.ProgramId,
            transaction.EventTime,
            notification.ObservedAt,
            transaction.Commitment,
            CanonicalStatus.Confirmed,
            transaction.Source,
            transaction.Json,
            notification.Signature);
        await rawEventStore.PersistAsync(input, cancellationToken);
    }
}

public sealed class SolanaDataUnavailableException(string signature, ulong slot)
    : Exception(
        $"Solana transaction '{signature}' at slot {slot} is temporarily unavailable.");

public sealed class UnsupportedProgramVersionException(
    string programId,
    string discriminator)
    : Exception(
        $"Unsupported instruction discriminator '{discriminator}' for program '{programId}'.")
{
    public string ProgramId { get; } = programId;

    public string Discriminator { get; } = discriminator;
}

public sealed class NullDiscoveryConnectionObserver : IDiscoveryConnectionObserver
{
    public ValueTask ConnectedAsync(
        string source,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisconnectedAsync(
        string source,
        string reason,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
