using System.Globalization;
using System.Text;

namespace CryptoIntelligence.Domain.Intelligence;

public sealed record ThemeRuleDefinition(
    IReadOnlyList<string> HotKeywords,
    IReadOnlyList<string> BlockedKeywords,
    bool RequiredMatch,
    bool CaseInsensitive,
    bool NormalizeWhitespace,
    DateTimeOffset? ValidUntil,
    string ConfigurationVersion);

public sealed record ThemeMatchResult(
    bool Matched,
    bool Blocked,
    bool ConfigurationValid,
    int ThemeScore,
    IReadOnlyList<string> MatchedThemes,
    IReadOnlyList<string> MatchReasons,
    DateTimeOffset InputAsOfTime,
    string ConfigurationVersion);

public static class ThemeRuleEvaluator
{
    public static ThemeMatchResult Evaluate(
        string? tokenName,
        string? symbol,
        DateTimeOffset asOfTime,
        ThemeRuleDefinition rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rules.ConfigurationVersion);
        var name = Normalize(tokenName, rules);
        var normalizedSymbol = Normalize(symbol, rules);
        var searchable = $"{name} {normalizedSymbol}".Trim();
        if (rules.ValidUntil is { } validUntil && asOfTime > validUntil)
        {
            return new ThemeMatchResult(
                Matched: false,
                Blocked: false,
                ConfigurationValid: false,
                ThemeScore: 0,
                MatchedThemes: [],
                MatchReasons: ["Theme configuration has expired."],
                asOfTime,
                rules.ConfigurationVersion);
        }

        var blocked = Match(searchable, rules.BlockedKeywords, rules);
        if (blocked.Count > 0)
        {
            return new ThemeMatchResult(
                Matched: false,
                Blocked: true,
                ConfigurationValid: true,
                ThemeScore: 0,
                MatchedThemes: [],
                MatchReasons:
                [
                    $"Blocked keyword matched: {string.Join(", ", blocked)}."
                ],
                asOfTime,
                rules.ConfigurationVersion);
        }

        var matched = Match(searchable, rules.HotKeywords, rules);
        return new ThemeMatchResult(
            Matched: matched.Count > 0,
            Blocked: false,
            ConfigurationValid: true,
            ThemeScore: matched.Count > 0 ? 100 : 0,
            MatchedThemes: matched,
            MatchReasons: matched.Count > 0
                ? [$"Hot keyword matched: {string.Join(", ", matched)}."]
                : ["No hot keyword matched."],
            asOfTime,
            rules.ConfigurationVersion);
    }

    private static IReadOnlyList<string> Match(
        string searchable,
        IReadOnlyList<string> keywords,
        ThemeRuleDefinition rules) =>
        keywords
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(value => ContainsTerm(
                searchable,
                Normalize(value, rules),
                rules.CaseInsensitive))
            .Select(value => value.Trim())
            .Distinct(
                rules.CaseInsensitive
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
            .Order(
                rules.CaseInsensitive
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal)
            .ToArray();

    private static bool ContainsTerm(
        string searchable,
        string keyword,
        bool caseInsensitive)
    {
        if (keyword.Length == 0)
        {
            return false;
        }

        var comparison = caseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return $" {searchable} ".Contains($" {keyword} ", comparison);
    }

    private static string Normalize(
        string? value,
        ThemeRuleDefinition rules)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        var result = rules.NormalizeWhitespace
            ? string.Join(
                ' ',
                builder.ToString().Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries))
            : builder.ToString().Trim();
        return rules.CaseInsensitive
            ? result.ToUpper(CultureInfo.InvariantCulture)
            : result;
    }
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RiskRuleOutcome
{
    Pass,
    Fail,
    Missing,
    NotApplicable
}

public enum SellQuoteStatus
{
    Available,
    TemporarilyUnavailable,
    StructurallyUnsupported,
    Stale
}

public sealed record SellQuoteEvidence(
    SellQuoteStatus Status,
    decimal InputBaseAmount,
    decimal OutputQuoteAmount,
    int PriceImpactBasisPoints,
    DateTimeOffset AsOfTime,
    string AdapterVersion,
    string? FailureReason);

public sealed record RiskEvidenceSnapshot(
    DateTimeOffset InputAsOfTime,
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
    SellQuoteEvidence? SellQuote);

public sealed record RiskPolicy(
    string RiskModelVersion,
    bool FormalRun,
    bool RequireSellQuote,
    bool RejectUnsupportedPoolVersion,
    bool RejectStaleMarketState,
    bool RejectNonFinalizedForFormalRun,
    bool RejectNonReconciledForFormalRun,
    bool RejectMintAuthorityRisk,
    bool RejectFreezeAuthorityRisk,
    decimal? MinimumQuoteReserveRaw,
    int? MaximumLiquidityDropBasisPoints,
    int? MaximumCreatorHoldingBasisPoints,
    int? MaximumTop10HoldingBasisPoints,
    int MaximumEntryPriceImpactBasisPoints,
    int MaximumMarketDataAgeSeconds);

public sealed record RiskRuleResult(
    string RuleId,
    RiskRuleOutcome Outcome,
    bool HardReject,
    int RiskScore,
    string Reason);

public sealed record RiskAssessment(
    int OverallScore,
    RiskLevel RiskLevel,
    bool HardReject,
    IReadOnlyList<RiskRuleResult> RuleResults,
    IReadOnlyList<string> Reasons,
    DateTimeOffset InputAsOfTime,
    string RiskModelVersion);

public static class MinimalRiskEvaluator
{
    public static RiskAssessment Evaluate(
        RiskEvidenceSnapshot evidence,
        RiskPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.RiskModelVersion);
        if (policy.MaximumEntryPriceImpactBasisPoints < 0 ||
            policy.MaximumMarketDataAgeSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }

        var rules = new List<RiskRuleResult>
        {
            SellQuote(evidence, policy),
            BooleanRisk(
                "pool-version",
                evidence.PoolVersionSupported,
                policy.RejectUnsupportedPoolVersion,
                "Pool/program version is supported.",
                "Pool/program version is unsupported."),
            Finality(evidence, policy),
            Reconciliation(evidence, policy),
            Staleness(evidence, policy),
            ThresholdMinimum(
                "quote-reserve",
                evidence.QuoteReserveRaw,
                policy.MinimumQuoteReserveRaw,
                "Quote reserve"),
            ThresholdMaximum(
                "liquidity-drop",
                evidence.LiquidityDropBasisPoints,
                policy.MaximumLiquidityDropBasisPoints,
                "Liquidity drop"),
            ThresholdMaximum(
                "entry-price-impact",
                evidence.EntryPriceImpactBasisPoints,
                policy.MaximumEntryPriceImpactBasisPoints,
                "Entry price impact"),
            NullableBooleanRisk(
                "mint-authority",
                evidence.MintAuthorityEnabled,
                policy.RejectMintAuthorityRisk,
                "Mint authority"),
            NullableBooleanRisk(
                "freeze-authority",
                evidence.FreezeAuthorityEnabled,
                policy.RejectFreezeAuthorityRisk,
                "Freeze authority"),
            NullableBooleanRisk(
                "adapter-authority",
                evidence.AdapterAuthorityRisk,
                enabled: true,
                "Adapter authority risk"),
            ThresholdMaximum(
                "creator-holding",
                evidence.CreatorHoldingBasisPoints,
                policy.MaximumCreatorHoldingBasisPoints,
                "Creator holding"),
            ThresholdMaximum(
                "top10-holding",
                evidence.Top10HoldingBasisPoints,
                policy.MaximumTop10HoldingBasisPoints,
                "Top 10 holder concentration")
        };
        var applicable = rules
            .Where(value => value.Outcome != RiskRuleOutcome.NotApplicable)
            .ToArray();
        var score = applicable.Length == 0
            ? 100
            : (int)Math.Round(
                applicable.Average(value => value.RiskScore),
                MidpointRounding.AwayFromZero);
        var hardReject = rules.Any(value => value.HardReject);
        var level = hardReject
            ? RiskLevel.Critical
            : score switch
            {
                < 25 => RiskLevel.Low,
                < 50 => RiskLevel.Medium,
                < 75 => RiskLevel.High,
                _ => RiskLevel.Critical
            };
        return new RiskAssessment(
            score,
            level,
            hardReject,
            rules,
            rules
                .Where(value => value.Outcome is
                    RiskRuleOutcome.Fail or RiskRuleOutcome.Missing)
                .Select(value => value.Reason)
                .ToArray(),
            evidence.InputAsOfTime,
            policy.RiskModelVersion);
    }

    private static RiskRuleResult SellQuote(
        RiskEvidenceSnapshot evidence,
        RiskPolicy policy)
    {
        if (!policy.RequireSellQuote)
        {
            return NotApplicable("sell-quote", "Sell quote is not required.");
        }

        if (evidence.SellQuote is null)
        {
            return Missing("sell-quote", "Sell quote evidence is missing.");
        }

        var age = evidence.InputAsOfTime - evidence.SellQuote.AsOfTime;
        if (age < TimeSpan.Zero ||
            age > TimeSpan.FromSeconds(policy.MaximumMarketDataAgeSeconds))
        {
            return Fail(
                "sell-quote",
                $"Sell quote age {age.TotalSeconds:F0}s is outside the allowed range.");
        }

        if (evidence.SellQuote.InputBaseAmount <= 0 ||
            string.IsNullOrWhiteSpace(evidence.SellQuote.AdapterVersion))
        {
            return Fail(
                "sell-quote",
                "Sell quote input amount or adapter version is invalid.");
        }

        return evidence.SellQuote.Status == SellQuoteStatus.Available &&
               evidence.SellQuote.OutputQuoteAmount > 0
            ? Pass("sell-quote", "A positive sell quote is available.")
            : Fail(
                "sell-quote",
                $"Sell quote is {evidence.SellQuote.Status}: " +
                $"{evidence.SellQuote.FailureReason ?? "no positive output"}.");
    }

    private static RiskRuleResult Finality(
        RiskEvidenceSnapshot evidence,
        RiskPolicy policy) =>
        !policy.FormalRun || !policy.RejectNonFinalizedForFormalRun
            ? NotApplicable("finality", "Finality is not required for this run.")
            : evidence.IsFinalized
                ? Pass("finality", "Input data is finalized.")
                : Fail("finality", "Formal run input is not finalized.");

    private static RiskRuleResult Reconciliation(
        RiskEvidenceSnapshot evidence,
        RiskPolicy policy) =>
        !policy.FormalRun || !policy.RejectNonReconciledForFormalRun
            ? NotApplicable(
                "reconciliation",
                "Reconciliation is not required for this run.")
            : evidence.IsReconciled
                ? Pass("reconciliation", "Input data is reconciled.")
                : Fail("reconciliation", "Formal run input is not reconciled.");

    private static RiskRuleResult Staleness(
        RiskEvidenceSnapshot evidence,
        RiskPolicy policy)
    {
        if (!policy.RejectStaleMarketState)
        {
            return NotApplicable("market-staleness", "Stale-state rejection is disabled.");
        }

        if (evidence.MarketAsOfTime is null)
        {
            return Missing("market-staleness", "Market snapshot time is missing.");
        }

        var age = evidence.InputAsOfTime - evidence.MarketAsOfTime.Value;
        return age < TimeSpan.Zero ||
               age > TimeSpan.FromSeconds(policy.MaximumMarketDataAgeSeconds)
            ? Fail(
                "market-staleness",
                $"Market snapshot age {age.TotalSeconds:F0}s is outside the allowed range.")
            : Pass("market-staleness", "Market snapshot is fresh.");
    }

    private static RiskRuleResult BooleanRisk(
        string ruleId,
        bool safe,
        bool enabled,
        string passReason,
        string failReason) =>
        !enabled
            ? NotApplicable(ruleId, "Rule is disabled.")
            : safe
                ? Pass(ruleId, passReason)
                : Fail(ruleId, failReason);

    private static RiskRuleResult NullableBooleanRisk(
        string ruleId,
        bool? risky,
        bool enabled,
        string label) =>
        !enabled
            ? NotApplicable(ruleId, $"{label} rule is disabled.")
            : risky is null
                ? Missing(ruleId, $"{label} evidence is missing.")
                : risky.Value
                    ? Fail(ruleId, $"{label} is enabled or unsafe.")
                    : Pass(ruleId, $"{label} is safe.");

    private static RiskRuleResult ThresholdMinimum<T>(
        string ruleId,
        T? actual,
        T? minimum,
        string label)
        where T : struct, IComparable<T> =>
        minimum is null
            ? NotApplicable(ruleId, $"{label} threshold is not configured.")
            : actual is null
                ? Missing(ruleId, $"{label} evidence is missing.")
                : actual.Value.CompareTo(minimum.Value) < 0
                    ? Fail(ruleId, $"{label} is below the configured minimum.")
                    : Pass(ruleId, $"{label} meets the configured minimum.");

    private static RiskRuleResult ThresholdMaximum<T>(
        string ruleId,
        T? actual,
        T? maximum,
        string label)
        where T : struct, IComparable<T> =>
        maximum is null
            ? NotApplicable(ruleId, $"{label} threshold is not configured.")
            : actual is null
                ? Missing(ruleId, $"{label} evidence is missing.")
                : actual.Value.CompareTo(maximum.Value) > 0
                    ? Fail(ruleId, $"{label} exceeds the configured maximum.")
                    : Pass(ruleId, $"{label} is within the configured maximum.");

    private static RiskRuleResult Pass(string id, string reason) =>
        new(id, RiskRuleOutcome.Pass, HardReject: false, RiskScore: 0, reason);

    private static RiskRuleResult Fail(string id, string reason) =>
        new(id, RiskRuleOutcome.Fail, HardReject: true, RiskScore: 100, reason);

    private static RiskRuleResult Missing(string id, string reason) =>
        new(id, RiskRuleOutcome.Missing, HardReject: true, RiskScore: 100, reason);

    private static RiskRuleResult NotApplicable(string id, string reason) =>
        new(
            id,
            RiskRuleOutcome.NotApplicable,
            HardReject: false,
            RiskScore: 0,
            reason);
}

public enum CandidateEligibilityStatus
{
    Observing,
    Eligible,
    Rejected,
    Expired
}

public sealed record CandidateEligibilityResult(
    CandidateEligibilityStatus Status,
    IReadOnlyList<string> Reasons,
    DateTimeOffset EvaluatedAt);

public static class CandidateEligibilityEvaluator
{
    public static CandidateEligibilityResult Evaluate(
        DateTimeOffset discoveredAt,
        DateTimeOffset evaluatedAt,
        TimeSpan minimumObservation,
        TimeSpan maximumEntryAge,
        bool hasUsableLiquidity,
        bool themeMatchRequired,
        ThemeMatchResult theme,
        RiskAssessment risk,
        int? maximumAllowedRiskScore)
    {
        if (evaluatedAt < discoveredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(evaluatedAt));
        }

        var age = evaluatedAt - discoveredAt;
        if (age > maximumEntryAge)
        {
            return new CandidateEligibilityResult(
                CandidateEligibilityStatus.Expired,
                ["Maximum entry age exceeded."],
                evaluatedAt);
        }

        var rejectionReasons = new List<string>();
        if (!theme.ConfigurationValid)
        {
            rejectionReasons.Add("Theme configuration is not valid at evaluation time.");
        }

        if (theme.Blocked)
        {
            rejectionReasons.AddRange(theme.MatchReasons);
        }
        else if (themeMatchRequired && !theme.Matched)
        {
            rejectionReasons.Add("A valid theme match is required.");
        }

        if (risk.HardReject)
        {
            rejectionReasons.AddRange(risk.Reasons);
        }

        if (maximumAllowedRiskScore is { } maximum &&
            risk.OverallScore > maximum)
        {
            rejectionReasons.Add(
                $"Risk score {risk.OverallScore} exceeds maximum {maximum}.");
        }

        if (rejectionReasons.Count > 0)
        {
            return new CandidateEligibilityResult(
                CandidateEligibilityStatus.Rejected,
                rejectionReasons.Distinct(StringComparer.Ordinal).ToArray(),
                evaluatedAt);
        }

        if (age < minimumObservation)
        {
            return new CandidateEligibilityResult(
                CandidateEligibilityStatus.Observing,
                ["Minimum observation window is not complete."],
                evaluatedAt);
        }

        if (!hasUsableLiquidity)
        {
            return new CandidateEligibilityResult(
                CandidateEligibilityStatus.Observing,
                ["Usable liquidity is not available yet."],
                evaluatedAt);
        }

        return new CandidateEligibilityResult(
            CandidateEligibilityStatus.Eligible,
            ["Theme, risk and minimum candidate conditions passed."],
            evaluatedAt);
    }
}
