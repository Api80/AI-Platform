using CryptoIntelligence.Domain.Radar;

namespace CryptoIntelligence.Application.Radar;

public sealed record RadarCandidateReadModel(
    string TokenAddress,
    string? Name,
    string? Symbol,
    CandidateStatus Status,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset UpdatedAt,
    string? Reason,
    int PoolCount,
    string? QuoteTokenAddress,
    string? LatestFeaturesJson);

public interface IRadarQueryService
{
    Task<IReadOnlyList<RadarCandidateReadModel>> ListCandidatesAsync(
        CandidateStatus? status,
        int limit,
        CancellationToken cancellationToken);

    Task<RadarCandidateReadModel?> FindCandidateAsync(
        string tokenAddress,
        CancellationToken cancellationToken);
}
