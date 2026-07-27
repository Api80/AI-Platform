using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Infrastructure.Persistence.Entities;

public sealed class RawBlockchainEventEntity
{
    public Guid Id { get; set; }
    public required string EventId { get; set; }
    public required string Chain { get; set; }
    public required string Network { get; set; }
    public long Slot { get; set; }
    public string? BlockHash { get; set; }
    public required string TransactionSignature { get; set; }
    public int InstructionIndex { get; set; }
    public int? InnerInstructionIndex { get; set; }
    public required string ProgramId { get; set; }
    public required string EventType { get; set; }
    public int EventOrdinal { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public DateTimeOffset ObservedTime { get; set; }
    public DateTimeOffset? FinalizedTime { get; set; }
    public required string CommitmentLevel { get; set; }
    public CanonicalStatus CanonicalStatus { get; set; }
    public DateTimeOffset FinalityUpdatedTime { get; set; }
    public DateTimeOffset? RevertedTime { get; set; }
    public string? RevertReason { get; set; }
    public required string Source { get; set; }
    public required string RawPayload { get; set; }
    public required string SchemaVersion { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? FirstFailureTime { get; set; }
    public DateTimeOffset? LastFailureTime { get; set; }
    public string? LastError { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

public sealed class IngestionCheckpointEntity
{
    public Guid Id { get; set; }
    public required string Chain { get; set; }
    public required string Network { get; set; }
    public required string Source { get; set; }
    public required string SubscriptionType { get; set; }
    public long ObservedThroughSlot { get; set; }
    public long PersistedThroughSlot { get; set; }
    public long ProcessedThroughSlot { get; set; }
    public long FinalizedThroughSlot { get; set; }
    public long ReconciledThroughSlot { get; set; }
    public string? LastCompletedSignature { get; set; }
    public required string Status { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

public sealed class IngestionSlotStateEntity
{
    public Guid Id { get; set; }
    public Guid CheckpointId { get; set; }
    public long Slot { get; set; }
    public bool Observed { get; set; }
    public bool Persisted { get; set; }
    public bool Processed { get; set; }
    public bool Finalized { get; set; }
    public bool Reconciled { get; set; }
    public bool HasGap { get; set; }
    public string? GapReason { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}
