using System.Text.Json;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Domain.Radar;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresRadarProjectionStore(
    CryptoIntelligenceDbContext context)
    : IRadarProjectionStore
{
    public async Task UpsertTokenAsync(
        TokenProjection token,
        CancellationToken cancellationToken)
    {
        var entity = await context.Tokens.SingleOrDefaultAsync(
            value =>
                value.Chain == token.Chain &&
                value.Network == token.Network &&
                value.MintAddress == token.MintAddress,
            cancellationToken);
        if (entity is null)
        {
            context.Tokens.Add(new TokenEntity
            {
                Id = Guid.NewGuid(),
                Chain = token.Chain,
                Network = token.Network,
                MintAddress = token.MintAddress,
                Name = token.Name,
                Symbol = token.Symbol,
                LifecycleStatus = token.Status,
                CreatedSlot = checked((long)token.CreatedSlot),
                CreatedTime = token.CreatedTime,
                FirstObservedTime = token.ObservedTime,
                UpdatedTime = token.ObservedTime
            });
        }
        else
        {
            entity.Name ??= token.Name;
            entity.Symbol ??= token.Symbol;
            if (token.Status > entity.LifecycleStatus)
            {
                entity.LifecycleStatus = token.Status;
            }

            entity.UpdatedTime = Max(entity.UpdatedTime, token.ObservedTime);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertPoolAsync(
        PoolProjection pool,
        CancellationToken cancellationToken)
    {
        var baseToken = await TokenAsync(pool.BaseMint, cancellationToken);
        var quoteToken = await TokenAsync(pool.QuoteMint, cancellationToken);
        var entity = await context.LiquidityPools.SingleOrDefaultAsync(
            value =>
                value.Chain == pool.Chain &&
                value.Network == pool.Network &&
                value.PoolAddress == pool.PoolAddress,
            cancellationToken);
        if (entity is null)
        {
            context.LiquidityPools.Add(new LiquidityPoolEntity
            {
                Id = Guid.NewGuid(),
                Chain = pool.Chain,
                Network = pool.Network,
                PoolAddress = pool.PoolAddress,
                Dex = pool.Dex,
                ProgramId = pool.ProgramId,
                BaseTokenId = baseToken.Id,
                QuoteTokenId = quoteToken.Id,
                CreatedSlot = checked((long)pool.CreatedSlot),
                CreatedTime = pool.CreatedTime,
                BaseReserve = pool.BaseReserve,
                QuoteReserve = pool.QuoteReserve,
                LifecycleStatus = pool.Status,
                FirstObservedTime = pool.CreatedTime,
                UpdatedTime = pool.CreatedTime
            });
        }
        else
        {
            entity.BaseReserve = pool.BaseReserve > 0
                ? pool.BaseReserve
                : entity.BaseReserve;
            entity.QuoteReserve = pool.QuoteReserve > 0
                ? pool.QuoteReserve
                : entity.QuoteReserve;
            if (pool.Status > entity.LifecycleStatus)
            {
                entity.LifecycleStatus = pool.Status;
            }

            entity.UpdatedTime = Max(entity.UpdatedTime, pool.CreatedTime);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendSwapAsync(
        SwapProjection swap,
        CancellationToken cancellationToken)
    {
        var pool = await PoolAsync(swap.PoolAddress, cancellationToken);
        var exists = await context.SwapEvents.AnyAsync(
            value =>
                value.RawEventId == swap.RawEventId &&
                value.PoolId == pool.Id &&
                value.SwapIndex == swap.SwapIndex,
            cancellationToken);
        if (exists)
        {
            return;
        }

        var wallet = string.IsNullOrWhiteSpace(swap.TraderWallet)
            ? null
            : await WalletAsync(
                swap.TraderWallet,
                swap.Slot,
                swap.EventTime,
                cancellationToken);
        var baseToken = await TokenAsync(swap.BaseMint, cancellationToken);
        var quoteToken = await TokenAsync(swap.QuoteMint, cancellationToken);
        context.SwapEvents.Add(new SwapEventEntity
        {
            Id = Guid.NewGuid(),
            RawEventId = swap.RawEventId,
            SwapIndex = swap.SwapIndex,
            PoolId = pool.Id,
            TraderWalletId = wallet?.Id,
            BaseTokenId = baseToken.Id,
            QuoteTokenId = quoteToken.Id,
            Side = swap.Side,
            BaseAmount = swap.BaseAmount,
            QuoteAmount = swap.QuoteAmount,
            PriceInQuote = swap.PriceInQuote,
            PriceImpactBasisPoints = swap.PriceImpactBasisPoints,
            Slot = checked((long)swap.Slot),
            EventTime = swap.EventTime,
            ObservedTime = swap.ObservedTime
        });
        context.MarketSnapshots.Add(new MarketSnapshotEntity
        {
            Id = Guid.NewGuid(),
            TokenId = baseToken.Id,
            PoolId = pool.Id,
            QuoteTokenId = quoteToken.Id,
            EventIndex = swap.SwapIndex,
            PriceInQuote = swap.PriceInQuote,
            BaseVolume = swap.BaseAmount,
            QuoteVolume = swap.QuoteAmount,
            BuyCount = swap.Side == SwapSide.Buy ? 1 : 0,
            SellCount = swap.Side == SwapSide.Sell ? 1 : 0,
            BaseReserve = pool.BaseReserve,
            QuoteReserve = pool.QuoteReserve,
            LiquidityInQuote = pool.QuoteReserve * 2,
            TraderAddress = swap.TraderWallet,
            PriceImpactBasisPoints = swap.PriceImpactBasisPoints,
            AsOfSlot = checked((long)swap.Slot),
            AsOfTime = swap.EventTime,
            CreatedTime = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendLiquidityAsync(
        LiquidityProjection liquidity,
        CancellationToken cancellationToken)
    {
        var pool = await PoolAsync(liquidity.PoolAddress, cancellationToken);
        var exists = await context.LiquidityEvents.AnyAsync(
            value =>
                value.RawEventId == liquidity.RawEventId &&
                value.PoolId == pool.Id &&
                value.LiquidityIndex == liquidity.LiquidityIndex,
            cancellationToken);
        if (exists)
        {
            return;
        }

        context.LiquidityEvents.Add(new LiquidityEventEntity
        {
            Id = Guid.NewGuid(),
            RawEventId = liquidity.RawEventId,
            LiquidityIndex = liquidity.LiquidityIndex,
            PoolId = pool.Id,
            ChangeType = liquidity.ChangeType,
            BaseAmount = liquidity.BaseAmount,
            QuoteAmount = liquidity.QuoteAmount,
            BaseReserveAfter = liquidity.BaseReserveAfter,
            QuoteReserveAfter = liquidity.QuoteReserveAfter,
            Slot = checked((long)liquidity.Slot),
            EventTime = liquidity.EventTime
        });
        if (liquidity.BaseReserveAfter > 0)
        {
            pool.BaseReserve = liquidity.BaseReserveAfter;
        }

        if (liquidity.QuoteReserveAfter > 0)
        {
            pool.QuoteReserve = liquidity.QuoteReserveAfter;
        }

        pool.UpdatedTime = Max(pool.UpdatedTime, liquidity.EventTime);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CandidateProjection?> GetCandidateAsync(
        string tokenAddress,
        CancellationToken cancellationToken)
    {
        var query =
            from candidate in context.TokenCandidates.AsNoTracking()
            join token in context.Tokens.AsNoTracking()
                on candidate.TokenId equals token.Id
            where token.MintAddress == tokenAddress &&
                  token.Chain == "Solana" &&
                  token.Network == "mainnet-beta"
            select new CandidateProjection(
                token.MintAddress,
                candidate.Status,
                candidate.DiscoveredAt,
                candidate.UpdatedAt,
                candidate.Reason);
        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertCandidateAsync(
        CandidateProjection candidate,
        CancellationToken cancellationToken)
    {
        var token = await TokenAsync(candidate.TokenAddress, cancellationToken);
        var entity = await context.TokenCandidates.SingleOrDefaultAsync(
            value => value.TokenId == token.Id,
            cancellationToken);
        if (entity is null)
        {
            context.TokenCandidates.Add(new TokenCandidateEntity
            {
                Id = Guid.NewGuid(),
                TokenId = token.Id,
                Status = candidate.Status,
                DiscoveredAt = candidate.DiscoveredAt,
                UpdatedAt = candidate.UpdatedAt,
                Reason = candidate.Reason
            });
        }
        else
        {
            entity.Status = candidate.Status;
            entity.UpdatedAt = candidate.UpdatedAt;
            entity.Reason = candidate.Reason;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketObservation>> LoadMarketObservationsAsync(
        string poolAddress,
        DateTimeOffset fromTime,
        DateTimeOffset toTime,
        CancellationToken cancellationToken)
    {
        var query =
            from snapshot in context.MarketSnapshots.AsNoTracking()
            join pool in context.LiquidityPools.AsNoTracking()
                on snapshot.PoolId equals pool.Id
            where pool.PoolAddress == poolAddress &&
                  snapshot.AsOfTime > fromTime &&
                  snapshot.AsOfTime <= toTime
            orderby snapshot.AsOfTime, snapshot.AsOfSlot, snapshot.EventIndex
            select new MarketObservation(
                checked((ulong)snapshot.AsOfSlot),
                snapshot.AsOfTime,
                snapshot.PriceInQuote,
                snapshot.BuyCount == 1
                    ? SwapSide.Buy
                    : snapshot.SellCount == 1
                        ? SwapSide.Sell
                        : SwapSide.Unknown,
                snapshot.BaseVolume,
                snapshot.QuoteVolume,
                snapshot.TraderAddress,
                snapshot.LiquidityInQuote,
                snapshot.PriceImpactBasisPoints);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task AppendFeatureAsync(
        FeatureProjection feature,
        CancellationToken cancellationToken)
    {
        var asOfSlot = checked((long)feature.AsOfSlot);
        var exists = await context.FeatureSnapshots.AnyAsync(
            value =>
                value.EntityType == feature.EntityType &&
                value.EntityNaturalKey == feature.EntityNaturalKey &&
                value.FeatureSetVersion == feature.FeatureSetVersion &&
                value.AsOfSlot == asOfSlot,
            cancellationToken);
        if (exists)
        {
            return;
        }

        context.FeatureSnapshots.Add(new FeatureSnapshotEntity
        {
            Id = Guid.NewGuid(),
            EntityType = feature.EntityType,
            EntityNaturalKey = feature.EntityNaturalKey,
            FeatureSetVersion = feature.FeatureSetVersion,
            AsOfSlot = asOfSlot,
            AsOfTime = feature.AsOfTime,
            ComputedTime = DateTimeOffset.UtcNow,
            Values = JsonSerializer.Serialize(feature.Values),
            SourceFromSlot = checked((long)feature.Values.SourceFromSlot),
            SourceToSlot = checked((long)feature.Values.SourceToSlot),
            SourceEventCount = feature.Values.SourceEventCount
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<TokenEntity> TokenAsync(
        string mint,
        CancellationToken cancellationToken) =>
        context.Tokens.SingleAsync(
            value =>
                value.Chain == "Solana" &&
                value.Network == "mainnet-beta" &&
                value.MintAddress == mint,
            cancellationToken);

    private Task<LiquidityPoolEntity> PoolAsync(
        string poolAddress,
        CancellationToken cancellationToken) =>
        context.LiquidityPools.SingleAsync(
            value =>
                value.Chain == "Solana" &&
                value.Network == "mainnet-beta" &&
                value.PoolAddress == poolAddress,
            cancellationToken);

    private async Task<WalletEntity> WalletAsync(
        string address,
        ulong slot,
        DateTimeOffset time,
        CancellationToken cancellationToken)
    {
        var wallet = await context.Wallets.SingleOrDefaultAsync(
            value =>
                value.Chain == "Solana" &&
                value.Network == "mainnet-beta" &&
                value.Address == address,
            cancellationToken);
        if (wallet is not null)
        {
            wallet.LastSeenTime = Max(wallet.LastSeenTime, time);
            wallet.UpdatedTime = DateTimeOffset.UtcNow;
            return wallet;
        }

        wallet = new WalletEntity
        {
            Id = Guid.NewGuid(),
            Chain = "Solana",
            Network = "mainnet-beta",
            Address = address,
            FirstSeenSlot = checked((long)slot),
            FirstSeenTime = time,
            LastSeenTime = time,
            CreatedTime = DateTimeOffset.UtcNow,
            UpdatedTime = DateTimeOffset.UtcNow
        };
        context.Wallets.Add(wallet);
        return wallet;
    }

    private static DateTimeOffset Max(
        DateTimeOffset first,
        DateTimeOffset second) => first >= second ? first : second;
}
