using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Application.Tests;

public sealed class ReconciliationTests
{
    [Fact]
    public async Task Cycle_persists_finalized_transactions_and_advances_checkpoint()
    {
        var store = new RecordingReconciliationStore(100);
        var raw = new RecordingRawStore();
        var service = new SolanaBackfillReconciliationService(
            new StubBackfillSource(
                102,
                new SolanaBackfillBatch(
                    100,
                    102,
                    true,
                    [
                        new SolanaBackfillSignature("a", 101, false, null),
                        new SolanaBackfillSignature("b", 102, false, null)
                    ])),
            new StubTransactionSource(),
            raw,
            store,
            "primary",
            maximumSlotsPerCycle: 10,
            maximumSignaturesPerCycle: 100);

        var result = await service.RunCycleAsync(
            "program",
            100,
            DateTimeOffset.UnixEpoch,
            CancellationToken.None);

        Assert.Equal(2, result.PersistedTransactionCount);
        Assert.Equal(102UL, result.Watermarks.ReconciledThroughSlot);
        Assert.All(raw.Inputs, value =>
        {
            Assert.Equal(CanonicalStatus.Finalized, value.CanonicalStatus);
            Assert.Equal("finalized", value.CommitmentLevel);
        });
        Assert.Equal(["a", "b"], store.PromotedSignatures);
    }

    [Fact]
    public async Task Missing_transaction_records_gap_and_blocks_reconciled_watermark()
    {
        var store = new RecordingReconciliationStore(100);
        var service = new SolanaBackfillReconciliationService(
            new StubBackfillSource(
                101,
                new SolanaBackfillBatch(
                    100,
                    101,
                    true,
                    [new SolanaBackfillSignature("missing", 101, false, null)])),
            new StubTransactionSource(returnNull: true),
            new RecordingRawStore(),
            store,
            "primary",
            maximumSlotsPerCycle: 10,
            maximumSignaturesPerCycle: 100);

        var result = await service.RunCycleAsync(
            "program",
            100,
            DateTimeOffset.UnixEpoch,
            CancellationToken.None);

        Assert.Equal(1, result.GapCount);
        Assert.Equal(100UL, result.Watermarks.ReconciledThroughSlot);
        Assert.Single(store.Gaps);
        Assert.Equal(101UL, store.Gaps[0].Slot);
    }

    [Fact]
    public async Task Later_success_resolves_temporary_gap()
    {
        var store = new RecordingReconciliationStore(100);
        var transaction = new SolanaTransactionPayload(
            "recover",
            101,
            DateTimeOffset.UnixEpoch,
            "finalized",
            "primary",
            """{"result":{}}""");
        var service = new SolanaBackfillReconciliationService(
            new StubBackfillSource(
                101,
                new SolanaBackfillBatch(
                    100,
                    101,
                    true,
                    [new SolanaBackfillSignature("recover", 101, false, null)])),
            new SequenceTransactionSource(null, transaction),
            new RecordingRawStore(),
            store,
            "primary",
            maximumSlotsPerCycle: 10,
            maximumSignaturesPerCycle: 100);

        var first = await service.RunCycleAsync(
            "program",
            100,
            DateTimeOffset.UnixEpoch,
            CancellationToken.None);
        var second = await service.RunCycleAsync(
            "program",
            100,
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            CancellationToken.None);

        Assert.Equal(1, first.GapCount);
        Assert.Equal(0, second.GapCount);
        Assert.Equal(101UL, second.Watermarks.ReconciledThroughSlot);
        Assert.Empty(store.Gaps);
    }

    private sealed class StubBackfillSource(
        ulong finalizedSlot,
        SolanaBackfillBatch batch)
        : ISolanaBackfillSource
    {
        public Task<ulong> GetFinalizedSlotAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(finalizedSlot);

        public Task<SolanaBackfillBatch> ListFinalizedSignaturesAsync(
            string programId,
            ulong fromExclusive,
            ulong toInclusive,
            int maximumSignatures,
            CancellationToken cancellationToken) =>
            Task.FromResult(batch);
    }

    private sealed class StubTransactionSource(bool returnNull = false)
        : ISolanaTransactionSource
    {
        public Task<SolanaTransactionPayload?> FetchAsync(
            string signature,
            string commitment,
            CancellationToken cancellationToken) =>
            Task.FromResult<SolanaTransactionPayload?>(
                returnNull
                    ? null
                    : new SolanaTransactionPayload(
                        signature,
                        signature == "a" ? 101UL : 102UL,
                        DateTimeOffset.UnixEpoch,
                        commitment,
                        "primary",
                        """{"result":{}}"""));
    }

    private sealed class SequenceTransactionSource(
        params SolanaTransactionPayload?[] values)
        : ISolanaTransactionSource
    {
        private readonly Queue<SolanaTransactionPayload?> _values = new(values);

        public Task<SolanaTransactionPayload?> FetchAsync(
            string signature,
            string commitment,
            CancellationToken cancellationToken) =>
            Task.FromResult(_values.Dequeue());
    }

    private sealed class RecordingRawStore : IRawEventStore
    {
        public List<RawBlockchainEventInput> Inputs { get; } = [];

        public Task<PersistedRawEvent> PersistAsync(
            RawBlockchainEventInput input,
            CancellationToken cancellationToken)
        {
            Inputs.Add(input);
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

    private sealed class RecordingReconciliationStore
        : IIngestionReconciliationStore
    {
        private readonly Guid _id = Guid.NewGuid();
        private readonly ulong _initial;
        private ulong _coverageTo;

        public RecordingReconciliationStore(ulong initial)
        {
            _initial = initial;
            _coverageTo = initial;
        }

        public List<IngestionGap> Gaps { get; } = [];

        public List<string> PromotedSignatures { get; } = [];

        public Task<IngestionCheckpointSnapshot> GetOrCreateAsync(
            IngestionCheckpointKey key,
            ulong initialThroughSlot,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot(key, _initial));

        public Task RecordRealtimeObservationAsync(
            IngestionCheckpointKey key,
            ulong slot,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordBackfillCoverageAsync(
            Guid checkpointId,
            ulong fromExclusive,
            ulong toInclusive,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            _coverageTo = toInclusive;
            return Task.CompletedTask;
        }

        public Task MarkGapAsync(
            Guid checkpointId,
            ulong slot,
            string reason,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            Gaps.Add(new IngestionGap(checkpointId, "program", slot, reason, now));
            return Task.CompletedTask;
        }

        public Task PromoteSignatureToFinalizedAsync(
            string signature,
            DateTimeOffset finalizedAt,
            CancellationToken cancellationToken)
        {
            PromotedSignatures.Add(signature);
            return Task.CompletedTask;
        }

        public Task ResolveGapAsync(
            Guid checkpointId,
            ulong slot,
            string reason,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            Gaps.RemoveAll(value =>
                value.Slot == slot &&
                string.Equals(value.Reason, reason, StringComparison.Ordinal));
            return Task.CompletedTask;
        }

        public Task<IngestionCheckpointSnapshot> RefreshAndAdvanceAsync(
            Guid checkpointId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var reconciled = Gaps.Count == 0 ? _coverageTo : _initial;
            return Task.FromResult(Snapshot(
                new IngestionCheckpointKey(
                    "Solana",
                    "mainnet-beta",
                    "primary",
                    "program"),
                reconciled));
        }

        public Task<IReadOnlyList<IngestionCheckpointSnapshot>> ListCheckpointsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IngestionCheckpointSnapshot>>([]);

        public Task<IReadOnlyList<IngestionGap>> ListGapsAsync(
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IngestionGap>>(Gaps);

        private IngestionCheckpointSnapshot Snapshot(
            IngestionCheckpointKey key,
            ulong through) => new(
            _id,
            key,
            new IngestionWatermarks(through, through, through, through, through),
            Gaps.Count == 0 ? "Healthy" : "Gapped",
            DateTimeOffset.UnixEpoch);
    }
}
