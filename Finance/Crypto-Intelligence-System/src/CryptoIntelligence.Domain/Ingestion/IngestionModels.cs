using System.Security.Cryptography;
using System.Text;

namespace CryptoIntelligence.Domain.Ingestion;

public enum CanonicalStatus
{
    Observed,
    Confirmed,
    Finalized,
    Reverted
}

public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    RetryableFailure,
    DeadLetter
}

public sealed record RawEventIdentity(
    string Chain,
    string Network,
    string TransactionSignature,
    int InstructionIndex,
    int? InnerInstructionIndex,
    string EventType,
    int EventOrdinal,
    string SchemaVersion)
{
    public string EventId => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalValue())));

    private string CanonicalValue() => string.Join(
        '\n',
        Chain,
        Network,
        TransactionSignature,
        InstructionIndex,
        InnerInstructionIndex?.ToString() ?? "-",
        EventType,
        EventOrdinal,
        SchemaVersion);
}

public sealed record IngestionWatermarks(
    ulong ObservedThroughSlot,
    ulong PersistedThroughSlot,
    ulong ProcessedThroughSlot,
    ulong FinalizedThroughSlot,
    ulong ReconciledThroughSlot)
{
    public IngestionWatermarks Advance(
        ulong observed,
        ulong persisted,
        ulong processed,
        ulong finalized,
        ulong reconciled,
        IReadOnlySet<ulong>? knownGapSlots = null)
    {
        var next = new IngestionWatermarks(
            Math.Max(ObservedThroughSlot, observed),
            Math.Max(PersistedThroughSlot, persisted),
            Math.Max(ProcessedThroughSlot, processed),
            Math.Max(FinalizedThroughSlot, finalized),
            Math.Max(ReconciledThroughSlot, reconciled));

        if (next.PersistedThroughSlot > next.ObservedThroughSlot ||
            next.ProcessedThroughSlot > next.PersistedThroughSlot ||
            next.FinalizedThroughSlot > next.ProcessedThroughSlot ||
            next.ReconciledThroughSlot > next.FinalizedThroughSlot)
        {
            throw new InvalidOperationException(
                "Watermarks must satisfy Observed >= Persisted >= Processed >= Finalized >= Reconciled.");
        }

        if (knownGapSlots is not null &&
            knownGapSlots.Any(slot =>
                slot > ReconciledThroughSlot && slot <= next.ReconciledThroughSlot))
        {
            throw new InvalidOperationException(
                "Reconciled watermark cannot advance across a known slot gap.");
        }

        return next;
    }
}

public sealed record SlotCompletion(
    ulong Slot,
    bool Observed,
    bool Persisted,
    bool Processed,
    bool Finalized,
    bool Reconciled,
    bool HasGap);

public static class CheckpointAdvancer
{
    public static IngestionWatermarks AdvanceContinuous(
        IngestionWatermarks current,
        IEnumerable<SlotCompletion> slotStates)
    {
        var states = slotStates
            .GroupBy(value => value.Slot)
            .ToDictionary(group => group.Key, group => group.Last());

        var observed = Advance(
            current.ObservedThroughSlot,
            states,
            static value => value.Observed);
        var persisted = Math.Min(
            observed,
            Advance(current.PersistedThroughSlot, states, static value => value.Persisted));
        var processed = Math.Min(
            persisted,
            Advance(current.ProcessedThroughSlot, states, static value => value.Processed));
        var finalized = Math.Min(
            processed,
            Advance(current.FinalizedThroughSlot, states, static value => value.Finalized));
        var reconciled = Math.Min(
            finalized,
            Advance(
                current.ReconciledThroughSlot,
                states,
                static value => value.Reconciled && !value.HasGap));

        return new IngestionWatermarks(
            observed,
            persisted,
            processed,
            finalized,
            reconciled);
    }

    private static ulong Advance(
        ulong current,
        IReadOnlyDictionary<ulong, SlotCompletion> states,
        Func<SlotCompletion, bool> completed)
    {
        var next = current;
        while (next < ulong.MaxValue &&
               states.TryGetValue(next + 1, out var state) &&
               completed(state))
        {
            next++;
        }

        return next;
    }
}

public sealed class ProcessingLease
{
    public ProcessingStatus Status { get; private set; } = ProcessingStatus.Pending;

    public string? Owner { get; private set; }

    public DateTimeOffset? Until { get; private set; }

    public int RetryCount { get; private set; }

    public string? LastError { get; private set; }

    public bool TryAcquire(string owner, DateTimeOffset now, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (Status is ProcessingStatus.Completed or ProcessingStatus.DeadLetter)
        {
            return false;
        }

        if (Status == ProcessingStatus.Processing && Until > now)
        {
            return false;
        }

        Status = ProcessingStatus.Processing;
        Owner = owner;
        Until = now.Add(duration);
        return true;
    }

    public void Complete(string owner)
    {
        EnsureOwner(owner);
        Status = ProcessingStatus.Completed;
        Owner = null;
        Until = null;
    }

    public void Fail(
        string owner,
        string error,
        int maximumRetries)
    {
        EnsureOwner(owner);
        RetryCount++;
        LastError = error;
        Status = RetryCount >= maximumRetries
            ? ProcessingStatus.DeadLetter
            : ProcessingStatus.RetryableFailure;
        Owner = null;
        Until = null;
    }

    private void EnsureOwner(string owner)
    {
        if (Status != ProcessingStatus.Processing ||
            !string.Equals(Owner, owner, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The processing lease is not owned by this worker.");
        }
    }
}
