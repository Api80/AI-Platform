using CryptoIntelligence.Domain.Radar;

namespace CryptoIntelligence.Domain.Tests;

public sealed class RadarModelTests
{
    [Fact]
    public void Candidate_becomes_eligible_only_after_observation_and_liquidity()
    {
        var discovered = DateTimeOffset.Parse("2026-07-28T00:00:00Z");
        var candidate = new TokenCandidateState("token", discovered);

        candidate.ObservePool(discovered.AddSeconds(5), hasUsableLiquidity: true);
        candidate.Evaluate(
            discovered.AddSeconds(29),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(10),
            hasUsableLiquidity: true);
        Assert.Equal(CandidateStatus.Observing, candidate.Status);

        candidate.Evaluate(
            discovered.AddSeconds(30),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(10),
            hasUsableLiquidity: true);
        Assert.Equal(CandidateStatus.Eligible, candidate.Status);
    }

    [Fact]
    public void Candidate_expires_after_maximum_age()
    {
        var discovered = DateTimeOffset.Parse("2026-07-28T00:00:00Z");
        var candidate = new TokenCandidateState("token", discovered);

        candidate.Evaluate(
            discovered.AddMinutes(11),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(10),
            hasUsableLiquidity: true);

        Assert.Equal(CandidateStatus.Expired, candidate.Status);
    }

    [Fact]
    public void Rolling_window_calculates_quote_scoped_market_features()
    {
        var asOf = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        MarketObservation[] observations =
        [
            new(
                100,
                asOf.AddSeconds(-50),
                2m,
                SwapSide.Buy,
                10m,
                20m,
                "wallet-a",
                1_000m,
                20),
            new(
                101,
                asOf.AddSeconds(-20),
                3m,
                SwapSide.Buy,
                5m,
                15m,
                "wallet-b",
                1_100m,
                40),
            new(
                102,
                asOf.AddSeconds(-10),
                4m,
                SwapSide.Sell,
                2m,
                8m,
                "wallet-c",
                1_200m,
                60)
        ];

        var result = RollingMarketWindow.Calculate(
            observations,
            asOf,
            TimeSpan.FromMinutes(1));

        Assert.Equal(10_000, result.PriceChangeBasisPoints);
        Assert.Equal(2, result.BuyCount);
        Assert.Equal(1, result.SellCount);
        Assert.Equal(2, result.UniqueBuyers);
        Assert.Equal(2_000, result.LiquidityChangeBasisPoints);
        Assert.Equal(10, result.NoTradeDurationSeconds);
        Assert.Equal(40, result.AveragePriceImpactBasisPoints);
    }

    [Fact]
    public void Rolling_window_excludes_future_and_expired_events()
    {
        var asOf = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        MarketObservation[] observations =
        [
            Observation(asOf.AddMinutes(-2), 1),
            Observation(asOf.AddSeconds(-30), 2),
            Observation(asOf.AddSeconds(1), 3)
        ];

        var result = RollingMarketWindow.Calculate(
            observations,
            asOf,
            TimeSpan.FromMinutes(1));

        Assert.Equal(1, result.SourceEventCount);
        Assert.Equal(2UL, result.SourceFromSlot);
    }

    private static MarketObservation Observation(
        DateTimeOffset time,
        ulong slot) => new(
        slot,
        time,
        1m,
        SwapSide.Buy,
        1m,
        1m,
        "wallet",
        1m,
        0);
}
