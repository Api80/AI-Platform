using System.Globalization;
using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Domain.Ingestion;
using CryptoIntelligence.Domain.Radar;

namespace CryptoIntelligence.Application.Radar;

public sealed record ProjectionEvent(
    Guid RawEventId,
    ulong Slot,
    DateTimeOffset EventTime,
    DateTimeOffset ObservedTime,
    ParsedAdapterEvent Event,
    CanonicalStatus CanonicalStatus = CanonicalStatus.Observed);

public sealed record TokenProjection(
    string Chain,
    string Network,
    string MintAddress,
    string? Name,
    string? Symbol,
    ulong CreatedSlot,
    DateTimeOffset CreatedTime,
    DateTimeOffset ObservedTime,
    TokenLifecycleStatus Status);

public sealed record PoolProjection(
    string Chain,
    string Network,
    string PoolAddress,
    string Dex,
    string ProgramId,
    string BaseMint,
    string QuoteMint,
    ulong CreatedSlot,
    DateTimeOffset CreatedTime,
    decimal BaseReserve,
    decimal QuoteReserve,
    PoolLifecycleStatus Status,
    string? CreatorAddress = null,
    string? AmmConfigAddress = null,
    string? BaseTokenProgramId = null,
    string? QuoteTokenProgramId = null);

public sealed record SwapProjection(
    Guid RawEventId,
    int SwapIndex,
    string PoolAddress,
    string BaseMint,
    string QuoteMint,
    string? TraderWallet,
    SwapSide Side,
    decimal BaseAmount,
    decimal QuoteAmount,
    decimal PriceInQuote,
    int PriceImpactBasisPoints,
    ulong Slot,
    DateTimeOffset EventTime,
    DateTimeOffset ObservedTime);

public sealed record LiquidityProjection(
    Guid RawEventId,
    int LiquidityIndex,
    string PoolAddress,
    string ChangeType,
    decimal BaseAmount,
    decimal QuoteAmount,
    decimal BaseReserveAfter,
    decimal QuoteReserveAfter,
    ulong Slot,
    DateTimeOffset EventTime);

public sealed record CandidateProjection(
    string TokenAddress,
    CandidateStatus Status,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset UpdatedAt,
    string? Reason);

public sealed record FeatureProjection(
    string EntityType,
    string EntityNaturalKey,
    string FeatureSetVersion,
    ulong AsOfSlot,
    DateTimeOffset AsOfTime,
    RollingMarketFeatures Values);

public interface IRadarProjectionStore
{
    Task UpsertTokenAsync(
        TokenProjection token,
        CancellationToken cancellationToken);

    Task UpsertPoolAsync(
        PoolProjection pool,
        CancellationToken cancellationToken);

    Task AppendSwapAsync(
        SwapProjection swap,
        CancellationToken cancellationToken);

    Task AppendLiquidityAsync(
        LiquidityProjection liquidity,
        CancellationToken cancellationToken);

    Task<CandidateProjection?> GetCandidateAsync(
        string tokenAddress,
        CancellationToken cancellationToken);

    Task UpsertCandidateAsync(
        CandidateProjection candidate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketObservation>> LoadMarketObservationsAsync(
        string poolAddress,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    Task AppendFeatureAsync(
        FeatureProjection feature,
        CancellationToken cancellationToken);
}

public interface IProjectionEventHandler
{
    Task HandleAsync(
        ProjectionEvent projectionEvent,
        CancellationToken cancellationToken);
}

public sealed class RadarProjectionHandler(
    IRadarProjectionStore store,
    MvpConfiguration configuration)
    : IProjectionEventHandler
{
    public async Task HandleAsync(
        ProjectionEvent projectionEvent,
        CancellationToken cancellationToken)
    {
        var attributes = projectionEvent.Event.Attributes;
        if (attributes is null)
        {
            return;
        }

        switch (projectionEvent.Event.DomainEventType)
        {
            case "MintCreated":
                await ProjectTokenAsync(projectionEvent, attributes, cancellationToken);
                break;
            case "PoolCreated":
                await ProjectPoolAsync(projectionEvent, attributes, cancellationToken);
                break;
            case "SwapObserved":
                await ProjectSwapAsync(projectionEvent, attributes, cancellationToken);
                break;
            case "LiquidityChanged":
                await ProjectLiquidityAsync(projectionEvent, attributes, cancellationToken);
                break;
        }
    }

    private async Task ProjectTokenAsync(
        ProjectionEvent value,
        IReadOnlyDictionary<string, string> attributes,
        CancellationToken cancellationToken)
    {
        if (!TryGet(attributes, "base_mint", out var mint))
        {
            return;
        }

        await store.UpsertTokenAsync(
            Token(mint, value, TokenLifecycleStatus.Discovered),
            cancellationToken);
        var candidate = await EnsureCandidateAsync(
            mint,
            value.EventTime,
            cancellationToken);
        await SaveCandidateAsync(candidate, cancellationToken);
    }

    private async Task ProjectPoolAsync(
        ProjectionEvent value,
        IReadOnlyDictionary<string, string> attributes,
        CancellationToken cancellationToken)
    {
        if (!TryGet(attributes, "pool_address", out var pool) ||
            !TryGet(attributes, "base_mint", out var baseMint) ||
            !TryGet(attributes, "quote_mint", out var quoteMint))
        {
            return;
        }

        await store.UpsertTokenAsync(
            Token(baseMint, value, TokenLifecycleStatus.PoolAvailable),
            cancellationToken);
        await store.UpsertTokenAsync(
            Token(quoteMint, value, TokenLifecycleStatus.PoolAvailable),
            cancellationToken);
        var baseReserve = Decimal(attributes, "base_reserve");
        var quoteReserve = Decimal(attributes, "quote_reserve");
        await store.UpsertPoolAsync(
            new PoolProjection(
                "Solana",
                "mainnet-beta",
                pool,
                "Raydium",
                value.Event.ProgramId,
                baseMint,
                quoteMint,
                value.Slot,
                value.EventTime,
                baseReserve,
                quoteReserve,
                baseReserve > 0 && quoteReserve > 0
                    ? PoolLifecycleStatus.Active
                    : PoolLifecycleStatus.Discovered,
                attributes.GetValueOrDefault("creator_address"),
                attributes.GetValueOrDefault("amm_config_address"),
                attributes.GetValueOrDefault("base_token_program_id"),
                attributes.GetValueOrDefault("quote_token_program_id")),
            cancellationToken);

        var candidate = await EnsureCandidateAsync(
            baseMint,
            value.EventTime,
            cancellationToken);
        candidate.ObservePool(
            value.EventTime,
            baseReserve > 0 && quoteReserve > 0);
        await SaveCandidateAsync(candidate, cancellationToken);
    }

    private async Task ProjectSwapAsync(
        ProjectionEvent value,
        IReadOnlyDictionary<string, string> attributes,
        CancellationToken cancellationToken)
    {
        if (!TryGet(attributes, "pool_address", out var pool) ||
            !TryGet(attributes, "base_mint", out var baseMint) ||
            !TryGet(attributes, "quote_mint", out var quoteMint))
        {
            return;
        }

        var baseAmount = Decimal(attributes, "base_amount");
        var quoteAmount = Decimal(attributes, "quote_amount");
        var price = baseAmount == 0 ? 0 : quoteAmount / baseAmount;
        var side = attributes.GetValueOrDefault("side")?.ToLowerInvariant() switch
        {
            "buy" => SwapSide.Buy,
            "sell" => SwapSide.Sell,
            _ => SwapSide.Unknown
        };
        await store.UpsertTokenAsync(
            Token(baseMint, value, TokenLifecycleStatus.Trading),
            cancellationToken);
        await store.UpsertTokenAsync(
            Token(quoteMint, value, TokenLifecycleStatus.Trading),
            cancellationToken);
        await store.UpsertPoolAsync(
            new PoolProjection(
                "Solana",
                "mainnet-beta",
                pool,
                "Raydium",
                value.Event.ProgramId,
                baseMint,
                quoteMint,
                value.Slot,
                value.EventTime,
                0,
                0,
                PoolLifecycleStatus.Active),
            cancellationToken);
        await store.AppendSwapAsync(
            new SwapProjection(
                value.RawEventId,
                value.Event.EventOrdinal,
                pool,
                baseMint,
                quoteMint,
                attributes.GetValueOrDefault("trader"),
                side,
                baseAmount,
                quoteAmount,
                price,
                Int(attributes, "price_impact_bps"),
                value.Slot,
                value.EventTime,
                value.ObservedTime),
            cancellationToken);

        await UpdateFeaturesAndCandidateAsync(
            pool,
            baseMint,
            value,
            cancellationToken);
    }

    private async Task ProjectLiquidityAsync(
        ProjectionEvent value,
        IReadOnlyDictionary<string, string> attributes,
        CancellationToken cancellationToken)
    {
        if (!TryGet(attributes, "pool_address", out var pool) ||
            !TryGet(attributes, "base_mint", out var baseMint) ||
            !TryGet(attributes, "quote_mint", out var quoteMint))
        {
            return;
        }

        await store.UpsertTokenAsync(
            Token(baseMint, value, TokenLifecycleStatus.PoolAvailable),
            cancellationToken);
        await store.UpsertTokenAsync(
            Token(quoteMint, value, TokenLifecycleStatus.PoolAvailable),
            cancellationToken);
        await store.UpsertPoolAsync(
            new PoolProjection(
                "Solana",
                "mainnet-beta",
                pool,
                "Raydium",
                value.Event.ProgramId,
                baseMint,
                quoteMint,
                value.Slot,
                value.EventTime,
                Decimal(attributes, "base_reserve"),
                Decimal(attributes, "quote_reserve"),
                PoolLifecycleStatus.Active),
            cancellationToken);
        await store.AppendLiquidityAsync(
            new LiquidityProjection(
                value.RawEventId,
                value.Event.EventOrdinal,
                pool,
                attributes.GetValueOrDefault("change_type") ?? "Updated",
                Decimal(attributes, "base_amount"),
                Decimal(attributes, "quote_amount"),
                Decimal(attributes, "base_reserve"),
                Decimal(attributes, "quote_reserve"),
                value.Slot,
                value.EventTime),
            cancellationToken);
    }

    private async Task UpdateFeaturesAndCandidateAsync(
        string pool,
        string baseMint,
        ProjectionEvent value,
        CancellationToken cancellationToken)
    {
        foreach (var windowSeconds in configuration.Radar.FeatureWindowsSeconds
                     .Distinct()
                     .Order())
        {
            var window = TimeSpan.FromSeconds(windowSeconds);
            var observations = await store.LoadMarketObservationsAsync(
                pool,
                value.EventTime - window,
                value.EventTime,
                cancellationToken);
            var features = RollingMarketWindow.Calculate(
                observations,
                value.EventTime,
                window);
            await store.AppendFeatureAsync(
                new FeatureProjection(
                    "Pool",
                    pool,
                    $"radar-market-v1-{windowSeconds}s",
                    value.Slot,
                    value.EventTime,
                    features),
                cancellationToken);
        }

        var candidate = await EnsureCandidateAsync(
            baseMint,
            value.EventTime,
            cancellationToken);
        if (candidate.Status == CandidateStatus.Discovered)
        {
            candidate.ObservePool(value.EventTime, hasUsableLiquidity: true);
        }

        candidate.Evaluate(
            value.EventTime,
            TimeSpan.FromSeconds(configuration.Radar.MinimumObservationSeconds),
            TimeSpan.FromSeconds(configuration.Radar.MaximumCandidateAgeSeconds),
            hasUsableLiquidity: true);
        await SaveCandidateAsync(candidate, cancellationToken);
    }

    private async Task<TokenCandidateState> EnsureCandidateAsync(
        string tokenAddress,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken)
    {
        var stored = await store.GetCandidateAsync(tokenAddress, cancellationToken);
        return stored is null
            ? new TokenCandidateState(tokenAddress, discoveredAt)
            : Restore(stored);
    }

    private Task SaveCandidateAsync(
        TokenCandidateState candidate,
        CancellationToken cancellationToken) =>
        store.UpsertCandidateAsync(
            new CandidateProjection(
                candidate.TokenAddress,
                candidate.Status,
                candidate.DiscoveredAt,
                candidate.UpdatedAt,
                candidate.Reason),
            cancellationToken);

    private static TokenCandidateState Restore(CandidateProjection stored)
    {
        var result = new TokenCandidateState(
            stored.TokenAddress,
            stored.DiscoveredAt);
        if (stored.Status is CandidateStatus.Observing or CandidateStatus.Eligible)
        {
            result.ObservePool(stored.UpdatedAt, hasUsableLiquidity: true);
            if (stored.Status == CandidateStatus.Eligible)
            {
                result.Evaluate(
                    stored.UpdatedAt,
                    TimeSpan.Zero,
                    TimeSpan.MaxValue,
                    hasUsableLiquidity: true);
            }
        }
        else if (stored.Status == CandidateStatus.Rejected)
        {
            result.Reject(stored.UpdatedAt, stored.Reason ?? "Rejected");
        }
        else if (stored.Status == CandidateStatus.Expired)
        {
            result.Evaluate(
                stored.UpdatedAt,
                TimeSpan.Zero,
                TimeSpan.Zero,
                hasUsableLiquidity: false);
        }

        return result;
    }

    private static TokenProjection Token(
        string mint,
        ProjectionEvent value,
        TokenLifecycleStatus status) => new(
        "Solana",
        "mainnet-beta",
        mint,
        value.Event.Attributes?.GetValueOrDefault("name"),
        value.Event.Attributes?.GetValueOrDefault("symbol"),
        value.Slot,
        value.EventTime,
        value.ObservedTime,
        status);

    private static bool TryGet(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out string value)
    {
        value = attributes.GetValueOrDefault(key) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static decimal Decimal(
        IReadOnlyDictionary<string, string> attributes,
        string key) =>
        decimal.TryParse(
            attributes.GetValueOrDefault(key),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;

    private static int Int(
        IReadOnlyDictionary<string, string> attributes,
        string key) =>
        int.TryParse(
            attributes.GetValueOrDefault(key),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
}

public interface IReplayClock
{
    DateTimeOffset UtcNow { get; }

    void AdvanceTo(DateTimeOffset timestamp);
}

public sealed class ReplayEngine(
    IEnumerable<IProjectionEventHandler> handlers,
    IReplayClock clock)
{
    public async Task ReplayAsync(
        IEnumerable<ProjectionEvent> events,
        CancellationToken cancellationToken)
    {
        foreach (var projectionEvent in events
                     .OrderBy(value => value.EventTime)
                     .ThenBy(value => value.Slot)
                     .ThenBy(value => value.Event.EventOrdinal))
        {
            clock.AdvanceTo(projectionEvent.EventTime);
            foreach (var handler in handlers)
            {
                await handler.HandleAsync(projectionEvent, cancellationToken);
            }
        }
    }
}
