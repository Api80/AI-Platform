using CryptoIntelligence.Domain.Radar;

namespace CryptoIntelligence.Infrastructure.Persistence.Entities;

public sealed class TokenEntity
{
    public Guid Id { get; set; }
    public required string Chain { get; set; }
    public required string Network { get; set; }
    public required string MintAddress { get; set; }
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public TokenLifecycleStatus LifecycleStatus { get; set; }
    public long CreatedSlot { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset FirstObservedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

public sealed class LiquidityPoolEntity
{
    public Guid Id { get; set; }
    public required string Chain { get; set; }
    public required string Network { get; set; }
    public required string PoolAddress { get; set; }
    public required string Dex { get; set; }
    public required string ProgramId { get; set; }
    public Guid BaseTokenId { get; set; }
    public Guid QuoteTokenId { get; set; }
    public long CreatedSlot { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public decimal BaseReserve { get; set; }
    public decimal QuoteReserve { get; set; }
    public PoolLifecycleStatus LifecycleStatus { get; set; }
    public DateTimeOffset FirstObservedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

public sealed class WalletEntity
{
    public Guid Id { get; set; }
    public required string Chain { get; set; }
    public required string Network { get; set; }
    public required string Address { get; set; }
    public long FirstSeenSlot { get; set; }
    public DateTimeOffset FirstSeenTime { get; set; }
    public DateTimeOffset LastSeenTime { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset UpdatedTime { get; set; }
}

public sealed class SwapEventEntity
{
    public Guid Id { get; set; }
    public Guid RawEventId { get; set; }
    public int SwapIndex { get; set; }
    public Guid PoolId { get; set; }
    public Guid? TraderWalletId { get; set; }
    public Guid BaseTokenId { get; set; }
    public Guid QuoteTokenId { get; set; }
    public SwapSide Side { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal QuoteAmount { get; set; }
    public decimal PriceInQuote { get; set; }
    public int PriceImpactBasisPoints { get; set; }
    public long Slot { get; set; }
    public DateTimeOffset EventTime { get; set; }
    public DateTimeOffset ObservedTime { get; set; }
}

public sealed class LiquidityEventEntity
{
    public Guid Id { get; set; }
    public Guid RawEventId { get; set; }
    public int LiquidityIndex { get; set; }
    public Guid PoolId { get; set; }
    public required string ChangeType { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal QuoteAmount { get; set; }
    public decimal BaseReserveAfter { get; set; }
    public decimal QuoteReserveAfter { get; set; }
    public long Slot { get; set; }
    public DateTimeOffset EventTime { get; set; }
}

public sealed class MarketSnapshotEntity
{
    public Guid Id { get; set; }
    public Guid TokenId { get; set; }
    public Guid PoolId { get; set; }
    public Guid QuoteTokenId { get; set; }
    public int EventIndex { get; set; }
    public decimal PriceInQuote { get; set; }
    public decimal BaseVolume { get; set; }
    public decimal QuoteVolume { get; set; }
    public int BuyCount { get; set; }
    public int SellCount { get; set; }
    public decimal BaseReserve { get; set; }
    public decimal QuoteReserve { get; set; }
    public decimal LiquidityInQuote { get; set; }
    public string? TraderAddress { get; set; }
    public int PriceImpactBasisPoints { get; set; }
    public long AsOfSlot { get; set; }
    public DateTimeOffset AsOfTime { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
}

public sealed class TokenCandidateEntity
{
    public Guid Id { get; set; }
    public Guid TokenId { get; set; }
    public CandidateStatus Status { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? Reason { get; set; }
}

public sealed class FeatureSnapshotEntity
{
    public Guid Id { get; set; }
    public required string EntityType { get; set; }
    public required string EntityNaturalKey { get; set; }
    public required string FeatureSetVersion { get; set; }
    public long AsOfSlot { get; set; }
    public DateTimeOffset AsOfTime { get; set; }
    public DateTimeOffset ComputedTime { get; set; }
    public required string Values { get; set; }
    public long SourceFromSlot { get; set; }
    public long SourceToSlot { get; set; }
    public int SourceEventCount { get; set; }
}
