namespace CryptoIntelligence.Contracts;

public sealed record IngestionCheckpointResponse(
    string Source,
    string SubscriptionType,
    ulong ObservedThroughSlot,
    ulong PersistedThroughSlot,
    ulong ProcessedThroughSlot,
    ulong FinalizedThroughSlot,
    ulong ReconciledThroughSlot,
    string Status,
    DateTimeOffset UpdatedTime);

public sealed record IngestionGapResponse(
    string SubscriptionType,
    ulong Slot,
    string Reason,
    DateTimeOffset UpdatedTime);
