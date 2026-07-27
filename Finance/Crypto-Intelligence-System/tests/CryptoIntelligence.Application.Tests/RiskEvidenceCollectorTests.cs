using System.Numerics;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Application.Tests;

public sealed class RiskEvidenceCollectorTests
{
    [Fact]
    public async Task Collector_preserves_missing_evidence_for_conservative_evaluation()
    {
        var now = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        var authority = new TokenAuthorityEvidence(
            EvidenceAvailability.TemporarilyUnavailable,
            "mint",
            null,
            null,
            null,
            null,
            null,
            null,
            now,
            "rpc unavailable");
        var holders = new HolderConcentrationEvidence(
            EvidenceAvailability.Missing,
            "mint",
            null,
            BigInteger.Parse("10000"),
            null,
            BigInteger.Parse("6000"),
            null,
            6000,
            100,
            now,
            "creator missing");
        var quote = new SellQuoteEvidence(
            SellQuoteStatus.Available,
            1000,
            900,
            30,
            now,
            "adapter-v1",
            null);
        var collector = new RiskEvidenceCollector(
            new StubQuoteSource(quote),
            new StubTokenSource(authority, holders));
        var input = new RiskEvidenceCollectionInput(
            "mint",
            null,
            new RaydiumCpmmPoolSnapshot(
                "pool",
                "program",
                "adapter-v1",
                "mint",
                "quote",
                "token-program",
                "token-program",
                10000,
                10000,
                25,
                5,
                100,
                now),
            1000,
            now,
            10000,
            30,
            0,
            AdapterAuthorityRisk: false,
            IsFinalized: true,
            IsReconciled: true);

        var result = await collector.CollectAsync(
            input,
            CancellationToken.None);

        Assert.Null(result.Snapshot.MintAuthorityEnabled);
        Assert.Null(result.Snapshot.FreezeAuthorityEnabled);
        Assert.Null(result.Snapshot.CreatorHoldingBasisPoints);
        Assert.Equal(6000, result.Snapshot.Top10HoldingBasisPoints);
        Assert.Same(quote, result.Snapshot.SellQuote);
    }

    private sealed class StubQuoteSource(SellQuoteEvidence quote)
        : IRaydiumSellQuoteEvidenceSource
    {
        public SellQuoteEvidence QuoteExactInput(
            RaydiumCpmmPoolSnapshot snapshot,
            BigInteger amountInRaw,
            DateTimeOffset evaluatedAt) => quote;
    }

    private sealed class StubTokenSource(
        TokenAuthorityEvidence authority,
        HolderConcentrationEvidence holders)
        : ISolanaTokenRiskEvidenceSource
    {
        public Task<TokenAuthorityEvidence> GetAuthorityAsync(
            string mintAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult(authority);

        public Task<HolderConcentrationEvidence> GetHolderConcentrationAsync(
            string mintAddress,
            string? creatorAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult(holders);
    }
}
