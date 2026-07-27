namespace CryptoIntelligence.Domain.Radar;

public enum TokenLifecycleStatus
{
    Discovered,
    PoolAvailable,
    Trading,
    Inactive,
    Closed
}

public enum PoolLifecycleStatus
{
    Discovered,
    Active,
    Inactive,
    Closed
}

public enum CandidateStatus
{
    Discovered,
    Observing,
    Eligible,
    Rejected,
    Expired
}

public enum SwapSide
{
    Buy,
    Sell,
    Unknown
}

public sealed class TokenCandidateState
{
    public TokenCandidateState(
        string tokenAddress,
        DateTimeOffset discoveredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenAddress);
        TokenAddress = tokenAddress;
        DiscoveredAt = discoveredAt;
        UpdatedAt = discoveredAt;
    }

    public string TokenAddress { get; }

    public CandidateStatus Status { get; private set; } = CandidateStatus.Discovered;

    public DateTimeOffset DiscoveredAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? Reason { get; private set; }

    public void ObservePool(
        DateTimeOffset at,
        bool hasUsableLiquidity)
    {
        EnsureNotTerminal();
        if (at < DiscoveredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(at));
        }

        if (Status == CandidateStatus.Discovered)
        {
            Status = CandidateStatus.Observing;
        }
        UpdatedAt = at;
        Reason = hasUsableLiquidity ? null : "Pool has no usable liquidity.";
    }

    public void Evaluate(
        DateTimeOffset at,
        TimeSpan minimumObservation,
        TimeSpan maximumAge,
        bool hasUsableLiquidity)
    {
        EnsureNotTerminal();
        var age = at - DiscoveredAt;
        if (age > maximumAge)
        {
            Status = CandidateStatus.Expired;
            Reason = "Maximum candidate age exceeded.";
        }
        else if (Status == CandidateStatus.Observing &&
                 age >= minimumObservation &&
                 hasUsableLiquidity)
        {
            Status = CandidateStatus.Eligible;
            Reason = null;
        }

        UpdatedAt = at;
    }

    public void Reject(DateTimeOffset at, string reason)
    {
        EnsureNotTerminal();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = CandidateStatus.Rejected;
        Reason = reason;
        UpdatedAt = at;
    }

    private void EnsureNotTerminal()
    {
        if (Status is CandidateStatus.Rejected or CandidateStatus.Expired)
        {
            throw new InvalidOperationException(
                $"Candidate is already in terminal state {Status}.");
        }
    }
}

public sealed record MarketObservation(
    ulong Slot,
    DateTimeOffset EventTime,
    decimal PriceInQuote,
    SwapSide Side,
    decimal BaseVolume,
    decimal QuoteVolume,
    string? TraderWallet,
    decimal LiquidityInQuote,
    int PriceImpactBasisPoints);

public sealed record RollingMarketFeatures(
    ulong SourceFromSlot,
    ulong SourceToSlot,
    int SourceEventCount,
    int PriceChangeBasisPoints,
    int BuyCount,
    int SellCount,
    decimal BuyBaseVolume,
    decimal SellBaseVolume,
    int UniqueBuyers,
    decimal TransactionsPerSecond,
    int LiquidityChangeBasisPoints,
    int NoTradeDurationSeconds,
    int AveragePriceImpactBasisPoints);

public static class RollingMarketWindow
{
    public static RollingMarketFeatures Calculate(
        IEnumerable<MarketObservation> observations,
        DateTimeOffset asOfTime,
        TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        var fromTime = asOfTime - window;
        var values = observations
            .Where(value =>
                value.EventTime > fromTime &&
                value.EventTime <= asOfTime)
            .OrderBy(value => value.EventTime)
            .ThenBy(value => value.Slot)
            .ToArray();
        if (values.Length == 0)
        {
            return new RollingMarketFeatures(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                checked((int)Math.Min(int.MaxValue, window.TotalSeconds)),
                0);
        }

        var first = values[0];
        var last = values[^1];
        var priceChange = first.PriceInQuote == 0
            ? 0
            : ToBasisPoints((last.PriceInQuote - first.PriceInQuote) / first.PriceInQuote);
        var liquidityChange = first.LiquidityInQuote == 0
            ? 0
            : ToBasisPoints(
                (last.LiquidityInQuote - first.LiquidityInQuote) /
                first.LiquidityInQuote);
        var buys = values.Where(value => value.Side == SwapSide.Buy).ToArray();
        var sells = values.Where(value => value.Side == SwapSide.Sell).ToArray();
        var noTradeDuration = Math.Max(
            0,
            checked((int)Math.Min(
                int.MaxValue,
                (asOfTime - last.EventTime).TotalSeconds)));

        return new RollingMarketFeatures(
            values.Min(value => value.Slot),
            values.Max(value => value.Slot),
            values.Length,
            priceChange,
            buys.Length,
            sells.Length,
            buys.Sum(value => value.BaseVolume),
            sells.Sum(value => value.BaseVolume),
            buys.Where(value => !string.IsNullOrWhiteSpace(value.TraderWallet))
                .Select(value => value.TraderWallet)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            values.Length / (decimal)window.TotalSeconds,
            liquidityChange,
            noTradeDuration,
            (int)Math.Round(
                values.Average(value => value.PriceImpactBasisPoints),
                MidpointRounding.AwayFromZero));
    }

    private static int ToBasisPoints(decimal ratio) => checked(
        (int)Math.Round(
            ratio * 10_000m,
            MidpointRounding.AwayFromZero));
}
