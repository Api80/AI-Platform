using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Application.Intelligence;

public sealed record IntelligenceEvaluationInput(
    string? TokenName,
    string? Symbol,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset InputAsOfTime,
    bool HasUsableLiquidity,
    RiskEvidenceSnapshot RiskEvidence);

public sealed record IntelligenceEvaluationResult(
    ThemeMatchResult Theme,
    RiskAssessment Risk,
    CandidateEligibilityResult Candidate);

public sealed class IntelligenceEvaluationService(MvpConfiguration configuration)
{
    public IntelligenceEvaluationResult Evaluate(
        IntelligenceEvaluationInput input)
    {
        if (input.RiskEvidence.InputAsOfTime != input.InputAsOfTime)
        {
            throw new ArgumentException(
                "Risk evidence and evaluation input must use the same AsOfTime.",
                nameof(input));
        }

        var theme = ThemeRuleEvaluator.Evaluate(
            input.TokenName,
            input.Symbol,
            input.InputAsOfTime,
            new ThemeRuleDefinition(
                configuration.Theme.HotKeywords,
                configuration.Theme.BlockedKeywords,
                configuration.Theme.RequiredThemeMatch,
                configuration.Theme.CaseInsensitive,
                configuration.Theme.NormalizeWhitespace,
                configuration.Theme.ThemeValidUntil,
                configuration.Theme.ConfigurationVersion));
        var hardReject = configuration.Risk.HardReject;
        var risk = MinimalRiskEvaluator.Evaluate(
            input.RiskEvidence,
            new RiskPolicy(
                configuration.Risk.ModelVersion,
                configuration.FormalRun,
                hardReject.RequireSellQuote,
                hardReject.RejectUnsupportedPoolVersion,
                hardReject.RejectStaleMarketState,
                hardReject.RejectNonFinalizedForFormalRun,
                hardReject.RejectNonReconciledForFormalRun,
                hardReject.RejectMintAuthorityRisk,
                hardReject.RejectFreezeAuthorityRisk,
                hardReject.MinimumQuoteReserveRaw,
                hardReject.MaximumLiquidityDropBasisPoints,
                hardReject.MaximumCreatorHoldingBasisPoints,
                hardReject.MaximumTop10HoldingBasisPoints,
                hardReject.MaximumEntryPriceImpactBasisPoints,
                hardReject.MaximumMarketDataAgeSeconds));
        var candidate = CandidateEligibilityEvaluator.Evaluate(
            input.DiscoveredAt,
            input.InputAsOfTime,
            TimeSpan.FromSeconds(configuration.Radar.MinimumObservationSeconds),
            TimeSpan.FromSeconds(configuration.Radar.MaximumEntryAgeSeconds),
            input.HasUsableLiquidity,
            configuration.Theme.RequiredThemeMatch,
            theme,
            risk,
            configuration.Risk.MaximumAllowedRiskScore);
        return new IntelligenceEvaluationResult(theme, risk, candidate);
    }
}
