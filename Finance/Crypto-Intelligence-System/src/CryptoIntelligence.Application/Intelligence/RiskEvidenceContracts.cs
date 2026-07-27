using System.Numerics;
using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Application.Intelligence;

public enum EvidenceAvailability
{
    Available,
    TemporarilyUnavailable,
    StructurallyUnsupported,
    Missing
}

public sealed record RaydiumCpmmPoolSnapshot(
    string PoolAddress,
    string ProgramId,
    string AdapterVersion,
    string InputMint,
    string OutputMint,
    string InputTokenProgramId,
    string OutputTokenProgramId,
    BigInteger InputReserveRaw,
    BigInteger OutputReserveRaw,
    int TradingFeeBasisPoints,
    int CreatorFeeBasisPoints,
    ulong AsOfSlot,
    DateTimeOffset AsOfTime);

public sealed record TokenAuthorityEvidence(
    EvidenceAvailability Availability,
    string MintAddress,
    bool? MintAuthorityEnabled,
    bool? FreezeAuthorityEnabled,
    string? MintAuthority,
    string? FreezeAuthority,
    string? TokenProgramId,
    ulong? AsOfSlot,
    DateTimeOffset ObservedAt,
    string? Reason);

public sealed record HolderConcentrationEvidence(
    EvidenceAvailability Availability,
    string MintAddress,
    string? CreatorAddress,
    BigInteger? TotalSupplyRaw,
    BigInteger? CreatorHoldingRaw,
    BigInteger? Top10HoldingRaw,
    int? CreatorHoldingBasisPoints,
    int? Top10HoldingBasisPoints,
    ulong? AsOfSlot,
    DateTimeOffset ObservedAt,
    string? Reason);

public interface IRaydiumSellQuoteEvidenceSource
{
    SellQuoteEvidence QuoteExactInput(
        RaydiumCpmmPoolSnapshot snapshot,
        BigInteger amountInRaw,
        DateTimeOffset evaluatedAt);
}

public interface ISolanaTokenRiskEvidenceSource
{
    Task<TokenAuthorityEvidence> GetAuthorityAsync(
        string mintAddress,
        CancellationToken cancellationToken);

    Task<HolderConcentrationEvidence> GetHolderConcentrationAsync(
        string mintAddress,
        string? creatorAddress,
        CancellationToken cancellationToken);
}

public sealed record RiskEvidenceCollectionInput(
    string MintAddress,
    string? CreatorAddress,
    RaydiumCpmmPoolSnapshot PoolSnapshot,
    BigInteger SellAmountRaw,
    DateTimeOffset InputAsOfTime,
    decimal? QuoteReserveRaw,
    int? EntryPriceImpactBasisPoints,
    int? LiquidityDropBasisPoints,
    bool AdapterAuthorityRisk,
    bool IsFinalized,
    bool IsReconciled);

public sealed record CollectedRiskEvidence(
    RiskEvidenceSnapshot Snapshot,
    TokenAuthorityEvidence Authority,
    HolderConcentrationEvidence Holders);

public sealed class RiskEvidenceCollector(
    IRaydiumSellQuoteEvidenceSource sellQuotes,
    ISolanaTokenRiskEvidenceSource tokenEvidence)
{
    public async Task<CollectedRiskEvidence> CollectAsync(
        RiskEvidenceCollectionInput input,
        CancellationToken cancellationToken)
    {
        var authorityTask = tokenEvidence.GetAuthorityAsync(
            input.MintAddress,
            cancellationToken);
        var holdersTask = tokenEvidence.GetHolderConcentrationAsync(
            input.MintAddress,
            input.CreatorAddress,
            cancellationToken);
        await Task.WhenAll(authorityTask, holdersTask);
        var authority = await authorityTask;
        var holders = await holdersTask;
        var quote = sellQuotes.QuoteExactInput(
            input.PoolSnapshot,
            input.SellAmountRaw,
            input.InputAsOfTime);
        return new CollectedRiskEvidence(
            new RiskEvidenceSnapshot(
                input.InputAsOfTime,
                input.PoolSnapshot.AsOfTime,
                input.QuoteReserveRaw,
                input.EntryPriceImpactBasisPoints,
                input.LiquidityDropBasisPoints,
                authority.MintAuthorityEnabled,
                authority.FreezeAuthorityEnabled,
                input.AdapterAuthorityRisk,
                holders.CreatorHoldingBasisPoints,
                holders.Top10HoldingBasisPoints,
                PoolVersionSupported:
                    quote.Status != SellQuoteStatus.StructurallyUnsupported,
                input.IsFinalized,
                input.IsReconciled,
                quote),
            authority,
            holders);
    }
}
