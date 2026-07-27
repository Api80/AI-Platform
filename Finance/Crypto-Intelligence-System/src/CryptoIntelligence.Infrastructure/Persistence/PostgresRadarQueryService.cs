using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Domain.Radar;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresRadarQueryService(
    CryptoIntelligenceDbContext context)
    : IRadarQueryService
{
    public async Task<IReadOnlyList<RadarCandidateReadModel>> ListCandidatesAsync(
        CandidateStatus? status,
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query =
            from candidate in context.TokenCandidates.AsNoTracking()
            join token in context.Tokens.AsNoTracking()
                on candidate.TokenId equals token.Id
            where status == null || candidate.Status == status
            orderby candidate.UpdatedAt descending
            select new { candidate, token };
        var rows = await query.Take(limit).ToListAsync(cancellationToken);
        var result = new List<RadarCandidateReadModel>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(await BuildAsync(row.candidate.Id, cancellationToken));
        }

        return result;
    }

    public async Task<RadarCandidateReadModel?> FindCandidateAsync(
        string tokenAddress,
        CancellationToken cancellationToken)
    {
        var id = await (
                from candidate in context.TokenCandidates.AsNoTracking()
                join token in context.Tokens.AsNoTracking()
                    on candidate.TokenId equals token.Id
                where token.MintAddress == tokenAddress
                select (Guid?)candidate.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return id.HasValue
            ? await BuildAsync(id.Value, cancellationToken)
            : null;
    }

    private async Task<RadarCandidateReadModel> BuildAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var core = await (
                from candidate in context.TokenCandidates.AsNoTracking()
                join token in context.Tokens.AsNoTracking()
                    on candidate.TokenId equals token.Id
                where candidate.Id == candidateId
                select new { candidate, token })
            .SingleAsync(cancellationToken);
        var pools = await context.LiquidityPools.AsNoTracking()
            .Where(value => value.BaseTokenId == core.token.Id)
            .ToListAsync(cancellationToken);
        var latestPool = pools
            .OrderByDescending(value => value.UpdatedTime)
            .FirstOrDefault();
        string? quoteMint = null;
        string? features = null;
        if (latestPool is not null)
        {
            quoteMint = await context.Tokens.AsNoTracking()
                .Where(value => value.Id == latestPool.QuoteTokenId)
                .Select(value => value.MintAddress)
                .SingleAsync(cancellationToken);
            features = await context.FeatureSnapshots.AsNoTracking()
                .Where(value =>
                    value.EntityType == "Pool" &&
                    value.EntityNaturalKey == latestPool.PoolAddress)
                .OrderByDescending(value => value.AsOfTime)
                .Select(value => value.Values)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new RadarCandidateReadModel(
            core.token.MintAddress,
            core.token.Name,
            core.token.Symbol,
            core.candidate.Status,
            core.candidate.DiscoveredAt,
            core.candidate.UpdatedAt,
            core.candidate.Reason,
            pools.Count,
            quoteMint,
            features);
    }
}
