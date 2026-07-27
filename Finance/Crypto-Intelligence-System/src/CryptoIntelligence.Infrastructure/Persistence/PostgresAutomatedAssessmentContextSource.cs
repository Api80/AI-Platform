using CryptoIntelligence.Application.Intelligence;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresAutomatedAssessmentContextSource(
    CryptoIntelligenceDbContext context)
    : IAutomatedAssessmentContextSource
{
    public async Task<AutomatedAssessmentContext?> LoadAsync(
        string poolAddress,
        string programId,
        ulong slot,
        CancellationToken cancellationToken)
    {
        var value = await (
                from pool in context.LiquidityPools.AsNoTracking()
                join baseToken in context.Tokens.AsNoTracking()
                    on pool.BaseTokenId equals baseToken.Id
                join quoteToken in context.Tokens.AsNoTracking()
                    on pool.QuoteTokenId equals quoteToken.Id
                join candidate in context.TokenCandidates.AsNoTracking()
                    on baseToken.Id equals candidate.TokenId
                where pool.PoolAddress == poolAddress &&
                      pool.Chain == "Solana" &&
                      pool.Network == "mainnet-beta"
                select new
                {
                    pool,
                    baseToken,
                    quoteToken,
                    candidate
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (value is null)
        {
            return null;
        }

        var databaseSlot = checked((long)slot);
        var reconciled = await (
                from state in context.IngestionSlotStates.AsNoTracking()
                join checkpoint in context.IngestionCheckpoints.AsNoTracking()
                    on state.CheckpointId equals checkpoint.Id
                where checkpoint.SubscriptionType == programId &&
                      state.Slot == databaseSlot &&
                      state.Reconciled &&
                      !state.HasGap
                select state.Id)
            .AnyAsync(cancellationToken);
        return new AutomatedAssessmentContext(
            value.baseToken.MintAddress,
            value.baseToken.Name,
            value.baseToken.Symbol,
            value.pool.CreatorAddress,
            value.candidate.DiscoveredAt,
            value.pool.ProgramId,
            value.baseToken.MintAddress,
            value.quoteToken.MintAddress,
            value.pool.AmmConfigAddress,
            reconciled);
    }
}
