using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Domain.Intelligence;
using CryptoIntelligence.Domain.Radar;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresIntelligenceAssessmentStore(
    CryptoIntelligenceDbContext context)
    : IIntelligenceAssessmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<StoredIntelligenceEvaluation> SaveAsync(
        string tokenAddress,
        IntelligenceEvaluationResult evaluation,
        RiskEvidenceSnapshot evidence,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenAddress);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(evidence);
        ValidateTimes(evaluation);
        if (evidence.InputAsOfTime != evaluation.Risk.InputAsOfTime)
        {
            throw new ArgumentException(
                "Risk evidence and evaluation must share one AsOfTime.",
                nameof(evidence));
        }

        var target = await (
                from candidate in context.TokenCandidates
                join token in context.Tokens
                    on candidate.TokenId equals token.Id
                where token.MintAddress == tokenAddress &&
                      token.Chain == "Solana" &&
                      token.Network == "mainnet-beta"
                select new { candidate, token })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Token candidate '{tokenAddress}' does not exist.");

        var theme = await context.ThemeMatches.SingleOrDefaultAsync(
            value =>
                value.TokenId == target.token.Id &&
                value.ConfigurationVersion ==
                evaluation.Theme.ConfigurationVersion &&
                value.InputAsOfTime == evaluation.Theme.InputAsOfTime,
            cancellationToken);
        var themeCreated = theme is null;
        if (theme is null)
        {
            theme = CreateTheme(target.token.Id, evaluation.Theme);
            context.ThemeMatches.Add(theme);
        }
        else
        {
            EnsureSameTheme(theme, evaluation.Theme);
        }

        var risk = await context.RiskAssessments.SingleOrDefaultAsync(
            value =>
                value.TokenId == target.token.Id &&
                value.RiskModelVersion == evaluation.Risk.RiskModelVersion &&
                value.InputAsOfTime == evaluation.Risk.InputAsOfTime,
            cancellationToken);
        var riskCreated = risk is null;
        if (risk is null)
        {
            risk = CreateRisk(target.token.Id, evaluation.Risk, evidence);
            context.RiskAssessments.Add(risk);
        }
        else
        {
            EnsureSameRisk(risk, evaluation.Risk, evidence);
        }

        var candidateStatus = ToCandidateStatus(evaluation.Candidate.Status);
        var candidateReason = LimitReason(
            string.Join("; ", evaluation.Candidate.Reasons));
        if (target.candidate.LatestEvaluationAsOfTime ==
            evaluation.Risk.InputAsOfTime &&
            target.candidate.LatestThemeMatchId == theme.Id &&
            target.candidate.LatestRiskAssessmentId == risk.Id &&
            (target.candidate.Status != candidateStatus ||
             target.candidate.Reason != candidateReason))
        {
            throw new InvalidOperationException(
                "Candidate evaluation identity already exists with different content.");
        }

        if (target.candidate.LatestEvaluationAsOfTime is null ||
            evaluation.Risk.InputAsOfTime >=
            target.candidate.LatestEvaluationAsOfTime)
        {
            target.candidate.LatestThemeMatchId = theme.Id;
            target.candidate.LatestRiskAssessmentId = risk.Id;
            target.candidate.LatestEvaluationAsOfTime =
                evaluation.Risk.InputAsOfTime;
            target.candidate.Status = candidateStatus;
            target.candidate.UpdatedAt = evaluation.Candidate.EvaluatedAt;
            target.candidate.Reason = candidateReason;
        }

        await context.SaveChangesAsync(cancellationToken);
        return new StoredIntelligenceEvaluation(
            theme.Id,
            risk.Id,
            themeCreated,
            riskCreated);
    }

    private static ThemeMatchEntity CreateTheme(
        Guid tokenId,
        ThemeMatchResult value) => new()
        {
            Id = Guid.NewGuid(),
            TokenId = tokenId,
            Matched = value.Matched,
            Blocked = value.Blocked,
            ConfigurationValid = value.ConfigurationValid,
            ThemeScore = value.ThemeScore,
            MatchedThemes = Serialize(value.MatchedThemes),
            MatchReasons = Serialize(value.MatchReasons),
            InputAsOfTime = value.InputAsOfTime,
            ConfigurationVersion = value.ConfigurationVersion,
            CreatedTime = DateTimeOffset.UtcNow
        };

    private static RiskAssessmentEntity CreateRisk(
        Guid tokenId,
        RiskAssessment value,
        RiskEvidenceSnapshot evidence) => new()
        {
            Id = Guid.NewGuid(),
            TokenId = tokenId,
            FeatureSnapshotId = null,
            OverallScore = value.OverallScore,
            RiskLevel = value.RiskLevel,
            HardReject = value.HardReject,
            RuleResults = Serialize(value.RuleResults),
            Reasons = Serialize(value.Reasons),
            Evidence = JsonSerializer.Serialize(evidence, JsonOptions),
            InputAsOfTime = value.InputAsOfTime,
            RiskModelVersion = value.RiskModelVersion,
            CreatedTime = DateTimeOffset.UtcNow
        };

    private static void EnsureSameTheme(
        ThemeMatchEntity stored,
        ThemeMatchResult value)
    {
        if (stored.Matched != value.Matched ||
            stored.Blocked != value.Blocked ||
            stored.ConfigurationValid != value.ConfigurationValid ||
            stored.ThemeScore != value.ThemeScore ||
            !JsonSequenceEquals<string>(
                stored.MatchedThemes,
                value.MatchedThemes) ||
            !JsonSequenceEquals<string>(
                stored.MatchReasons,
                value.MatchReasons))
        {
            throw new InvalidOperationException(
                "Theme evaluation identity already exists with different content.");
        }
    }

    private static void EnsureSameRisk(
        RiskAssessmentEntity stored,
        RiskAssessment value,
        RiskEvidenceSnapshot evidence)
    {
        if (stored.OverallScore != value.OverallScore ||
            stored.RiskLevel != value.RiskLevel ||
            stored.HardReject != value.HardReject ||
            !JsonSequenceEquals<RiskRuleResult>(
                stored.RuleResults,
                value.RuleResults) ||
            !JsonSequenceEquals<string>(stored.Reasons, value.Reasons) ||
            stored.Evidence is not null &&
            !JsonObjectEquals(stored.Evidence, evidence))
        {
            throw new InvalidOperationException(
                "Risk evaluation identity already exists with different content.");
        }
    }

    private static bool JsonObjectEquals<T>(string json, T expected)
    {
        var stored = JsonSerializer.Deserialize<T>(json, JsonOptions);
        return EqualityComparer<T>.Default.Equals(stored, expected);
    }

    private static void ValidateTimes(IntelligenceEvaluationResult evaluation)
    {
        if (evaluation.Theme.InputAsOfTime != evaluation.Risk.InputAsOfTime ||
            evaluation.Theme.InputAsOfTime != evaluation.Candidate.EvaluatedAt)
        {
            throw new ArgumentException(
                "Theme, risk, and candidate evaluation must share one AsOfTime.",
                nameof(evaluation));
        }
    }

    private static CandidateStatus ToCandidateStatus(
        CandidateEligibilityStatus status) => status switch
        {
            CandidateEligibilityStatus.Observing => CandidateStatus.Observing,
            CandidateEligibilityStatus.Eligible => CandidateStatus.Eligible,
            CandidateEligibilityStatus.Rejected => CandidateStatus.Rejected,
            CandidateEligibilityStatus.Expired => CandidateStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    private static string? LimitReason(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= 1_000
                ? value
                : value[..1_000];

    private static string Serialize<T>(IReadOnlyList<T> value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static bool JsonSequenceEquals<T>(
        string json,
        IReadOnlyList<T> expected)
    {
        var stored = JsonSerializer.Deserialize<T[]>(json, JsonOptions)
                     ?? [];
        return stored.SequenceEqual(expected);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
