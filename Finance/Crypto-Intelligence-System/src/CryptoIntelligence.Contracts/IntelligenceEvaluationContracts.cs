namespace CryptoIntelligence.Contracts;

public sealed record SellQuoteEvidenceRequest(
    string Status,
    decimal InputBaseAmount,
    decimal OutputQuoteAmount,
    int PriceImpactBasisPoints,
    DateTimeOffset AsOfTime,
    string AdapterVersion,
    string? FailureReason);

public sealed record IntelligenceEvaluationRequest(
    string? TokenName,
    string? Symbol,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset InputAsOfTime,
    bool HasUsableLiquidity,
    DateTimeOffset? MarketAsOfTime,
    decimal? QuoteReserveRaw,
    int? EntryPriceImpactBasisPoints,
    int? LiquidityDropBasisPoints,
    bool? MintAuthorityEnabled,
    bool? FreezeAuthorityEnabled,
    bool? AdapterAuthorityRisk,
    int? CreatorHoldingBasisPoints,
    int? Top10HoldingBasisPoints,
    bool PoolVersionSupported,
    bool IsFinalized,
    bool IsReconciled,
    SellQuoteEvidenceRequest? SellQuote);

public sealed record ThemeMatchResponse(
    bool Matched,
    bool Blocked,
    bool ConfigurationValid,
    int ThemeScore,
    IReadOnlyList<string> MatchedThemes,
    IReadOnlyList<string> MatchReasons,
    DateTimeOffset InputAsOfTime,
    string ConfigurationVersion);

public sealed record RiskRuleResponse(
    string RuleId,
    string Outcome,
    bool HardReject,
    int RiskScore,
    string Reason);

public sealed record RiskAssessmentResponse(
    int OverallScore,
    string RiskLevel,
    bool HardReject,
    IReadOnlyList<RiskRuleResponse> RuleResults,
    IReadOnlyList<string> Reasons,
    DateTimeOffset InputAsOfTime,
    string RiskModelVersion);

public sealed record CandidateEligibilityResponse(
    string Status,
    IReadOnlyList<string> Reasons,
    DateTimeOffset EvaluatedAt);

public sealed record IntelligenceEvaluationResponse(
    ThemeMatchResponse Theme,
    RiskAssessmentResponse Risk,
    CandidateEligibilityResponse Candidate);
