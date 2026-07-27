namespace CryptoIntelligence.Contracts;

public sealed record RadarCandidateResponse(
    string TokenAddress,
    string? Name,
    string? Symbol,
    string Status,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset UpdatedAt,
    string? Reason,
    int PoolCount,
    string? QuoteTokenAddress,
    string? LatestFeaturesJson,
    ThemeMatchResponse? LatestTheme,
    RiskAssessmentResponse? LatestRisk);
