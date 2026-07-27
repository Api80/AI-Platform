using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Domain.Intelligence;
using CryptoIntelligence.Domain.Radar;
using CryptoIntelligence.Infrastructure.Persistence;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Tests;

public sealed class PostgresIntelligenceAssessmentStoreTests
{
    [Fact]
    [Trait("Category", "Postgres")]
    public async Task Save_is_idempotent_and_radar_returns_latest_explanation()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CRYPTO_TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<CryptoIntelligenceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new CryptoIntelligenceDbContext(options);
        await using var transaction =
            await context.Database.BeginTransactionAsync();
        var now = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        var mint = $"integration-{Guid.NewGuid():N}";
        var token = new TokenEntity
        {
            Id = Guid.NewGuid(),
            Chain = "Solana",
            Network = "mainnet-beta",
            MintAddress = mint,
            Name = "Example AI",
            Symbol = "EAI",
            LifecycleStatus = TokenLifecycleStatus.Trading,
            CreatedSlot = 100,
            CreatedTime = now.AddMinutes(-1),
            FirstObservedTime = now.AddMinutes(-1),
            UpdatedTime = now
        };
        var candidate = new TokenCandidateEntity
        {
            Id = Guid.NewGuid(),
            TokenId = token.Id,
            Status = CandidateStatus.Observing,
            DiscoveredAt = now.AddMinutes(-1),
            UpdatedAt = now.AddMinutes(-1)
        };
        context.Tokens.Add(token);
        context.TokenCandidates.Add(candidate);
        await context.SaveChangesAsync();

        var evaluation = Evaluation(now);
        var store = new PostgresIntelligenceAssessmentStore(context);
        var first = await store.SaveAsync(
            mint,
            evaluation,
            CancellationToken.None);
        var repeated = await store.SaveAsync(
            mint,
            evaluation,
            CancellationToken.None);

        Assert.True(first.ThemeCreated);
        Assert.True(first.RiskCreated);
        Assert.False(repeated.ThemeCreated);
        Assert.False(repeated.RiskCreated);
        Assert.Equal(first.ThemeMatchId, repeated.ThemeMatchId);
        Assert.Equal(first.RiskAssessmentId, repeated.RiskAssessmentId);
        Assert.Equal(
            1,
            await context.ThemeMatches.CountAsync(value =>
                value.TokenId == token.Id));
        Assert.Equal(
            1,
            await context.RiskAssessments.CountAsync(value =>
                value.TokenId == token.Id));

        var query = new PostgresRadarQueryService(context);
        var readModel = await query.FindCandidateAsync(
            mint,
            CancellationToken.None);

        Assert.NotNull(readModel);
        Assert.Equal(CandidateStatus.Eligible, readModel.Status);
        Assert.Equal("theme-v1", readModel.LatestTheme?.ConfigurationVersion);
        Assert.Equal("risk-v1", readModel.LatestRisk?.RiskModelVersion);
        Assert.False(readModel.LatestRisk?.HardReject);

        var conflicting = evaluation with
        {
            Risk = evaluation.Risk with { OverallScore = 10 }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(mint, conflicting, CancellationToken.None));

        var conflictingCandidate = evaluation with
        {
            Candidate = new CandidateEligibilityResult(
                CandidateEligibilityStatus.Rejected,
                ["Different result."],
                now)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveAsync(
                mint,
                conflictingCandidate,
                CancellationToken.None));

        await transaction.RollbackAsync();
    }

    private static IntelligenceEvaluationResult Evaluation(
        DateTimeOffset asOfTime) => new(
        new ThemeMatchResult(
            Matched: true,
            Blocked: false,
            ConfigurationValid: true,
            ThemeScore: 100,
            MatchedThemes: ["AI"],
            MatchReasons: ["Hot keyword matched: AI."],
            asOfTime,
            "theme-v1"),
        new RiskAssessment(
            OverallScore: 0,
            RiskLevel.Low,
            HardReject: false,
            RuleResults:
            [
                new RiskRuleResult(
                    "sell-quote",
                    RiskRuleOutcome.Pass,
                    HardReject: false,
                    RiskScore: 0,
                    "Sell quote is available.")
            ],
            Reasons: [],
            asOfTime,
            "risk-v1"),
        new CandidateEligibilityResult(
            CandidateEligibilityStatus.Eligible,
            ["Candidate passed theme and risk evaluation."],
            asOfTime));
}
