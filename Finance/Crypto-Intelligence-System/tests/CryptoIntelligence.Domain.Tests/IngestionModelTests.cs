using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Domain.Tests;

public sealed class IngestionModelTests
{
    [Fact]
    public void Event_identity_is_deterministic_and_includes_ordinal()
    {
        var first = Identity(0);
        var duplicate = Identity(0);
        var nextEvent = Identity(1);

        Assert.Equal(first.EventId, duplicate.EventId);
        Assert.NotEqual(first.EventId, nextEvent.EventId);
        Assert.Equal(64, first.EventId.Length);
    }

    [Fact]
    public void Watermarks_cannot_cross_a_known_gap()
    {
        var current = new IngestionWatermarks(10, 10, 10, 10, 8);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            current.Advance(12, 12, 12, 12, 12, new HashSet<ulong> { 11 }));

        Assert.Contains("gap", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Checkpoint_advances_only_over_contiguous_completed_slots()
    {
        var current = new IngestionWatermarks(100, 100, 100, 100, 100);
        SlotCompletion[] states =
        [
            new(101, true, true, true, true, true, false),
            new(102, true, true, true, true, false, true),
            new(103, true, true, true, true, true, false)
        ];

        var result = CheckpointAdvancer.AdvanceContinuous(current, states);

        Assert.Equal(103UL, result.ObservedThroughSlot);
        Assert.Equal(103UL, result.FinalizedThroughSlot);
        Assert.Equal(101UL, result.ReconciledThroughSlot);
    }

    [Fact]
    public void Expired_processing_lease_can_be_recovered()
    {
        var lease = new ProcessingLease();
        var now = DateTimeOffset.Parse("2026-07-28T00:00:00Z");

        Assert.True(lease.TryAcquire("worker-a", now, TimeSpan.FromSeconds(30)));
        Assert.False(lease.TryAcquire("worker-b", now.AddSeconds(29), TimeSpan.FromSeconds(30)));
        Assert.True(lease.TryAcquire("worker-b", now.AddSeconds(31), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Failure_moves_to_dead_letter_at_retry_limit()
    {
        var lease = new ProcessingLease();
        var now = DateTimeOffset.Parse("2026-07-28T00:00:00Z");

        Assert.True(lease.TryAcquire("worker", now, TimeSpan.FromMinutes(1)));
        lease.Fail("worker", "temporary", maximumRetries: 1);

        Assert.Equal(ProcessingStatus.DeadLetter, lease.Status);
        Assert.Equal(1, lease.RetryCount);
    }

    private static RawEventIdentity Identity(int ordinal) => new(
        "Solana",
        "mainnet-beta",
        "5XxcFXGiK47v7GkqM22J2LA2Bst46Mm2Lm4ob1YuyZzTaziXmimD66EiM9JQFZi9A3GPg8mcZrJSgKyQTBJUgTGN",
        2,
        1,
        "SwapObserved",
        ordinal,
        "raw-event-v1");
}
