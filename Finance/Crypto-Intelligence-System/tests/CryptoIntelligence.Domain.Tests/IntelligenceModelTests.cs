using CryptoIntelligence.Domain.Intelligence;
using System.Numerics;

namespace CryptoIntelligence.Domain.Tests;

public sealed class IntelligenceModelTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-07-28T00:01:00Z");

    [Fact]
    public void Cpmm_quote_matches_pinned_raydium_sdk_vectors()
    {
        var buy = CpmmExactInputQuoteCalculator.Calculate(
            BigInteger.Parse("12404532310903"),
            BigInteger.Parse("16137545623432"),
            BigInteger.Parse("100000000"),
            tradingFeeBasisPoints: 25,
            creatorFeeBasisPoints: 5);
        var sell = CpmmExactInputQuoteCalculator.Calculate(
            BigInteger.Parse("16137545623432"),
            BigInteger.Parse("12404532310903"),
            BigInteger.Parse("1000000000"),
            tradingFeeBasisPoints: 25,
            creatorFeeBasisPoints: 5);

        Assert.Equal(BigInteger.Parse("129702622"), buy.AmountOutRaw);
        Assert.Equal(BigInteger.Parse("766321904"), sell.AmountOutRaw);
        Assert.Equal(30, buy.TotalImpactBasisPoints);
        Assert.Equal(30, sell.TotalImpactBasisPoints);
    }

    [Fact]
    public void Theme_match_normalizes_case_punctuation_and_whitespace()
    {
        var result = ThemeRuleEvaluator.Evaluate(
            "  Next-Gen   ai token ",
            "NGAI",
            Now,
            ThemeRules(hot: ["AI TOKEN"]));

        Assert.True(result.Matched);
        Assert.Equal(100, result.ThemeScore);
        Assert.Equal(["AI TOKEN"], result.MatchedThemes);
    }

    [Fact]
    public void Blocked_keyword_has_priority_over_hot_keyword()
    {
        var result = ThemeRuleEvaluator.Evaluate(
            "AI Scam",
            "AIS",
            Now,
            ThemeRules(hot: ["AI"], blocked: ["scam"]));

        Assert.True(result.Blocked);
        Assert.False(result.Matched);
        Assert.Empty(result.MatchedThemes);
    }

    [Fact]
    public void Expired_theme_configuration_is_not_valid()
    {
        var rules = ThemeRules(hot: ["AI"]) with
        {
            ValidUntil = Now.AddSeconds(-1)
        };

        var result = ThemeRuleEvaluator.Evaluate("AI", "AI", Now, rules);

        Assert.False(result.ConfigurationValid);
        Assert.False(result.Matched);
    }

    [Fact]
    public void Missing_required_evidence_is_a_hard_reject()
    {
        var result = MinimalRiskEvaluator.Evaluate(
            Evidence() with
            {
                SellQuote = null,
                MintAuthorityEnabled = null
            },
            Policy());

        Assert.True(result.HardReject);
        Assert.Equal(RiskLevel.Critical, result.RiskLevel);
        Assert.Contains(
            result.RuleResults,
            rule =>
                rule.RuleId == "sell-quote" &&
                rule.Outcome == RiskRuleOutcome.Missing);
        Assert.Contains(
            result.RuleResults,
            rule =>
                rule.RuleId == "mint-authority" &&
                rule.Outcome == RiskRuleOutcome.Missing);
    }

    [Fact]
    public void Risk_threshold_boundaries_pass()
    {
        var result = MinimalRiskEvaluator.Evaluate(
            Evidence() with
            {
                QuoteReserveRaw = 1_000,
                EntryPriceImpactBasisPoints = 1_000,
                CreatorHoldingBasisPoints = 2_000,
                Top10HoldingBasisPoints = 6_000
            },
            Policy() with
            {
                MinimumQuoteReserveRaw = 1_000,
                MaximumCreatorHoldingBasisPoints = 2_000,
                MaximumTop10HoldingBasisPoints = 6_000,
                MaximumEntryPriceImpactBasisPoints = 1_000
            });

        Assert.False(result.HardReject);
        Assert.Equal(0, result.OverallScore);
        Assert.All(
            result.RuleResults.Where(rule =>
                rule.Outcome != RiskRuleOutcome.NotApplicable),
            rule => Assert.Equal(RiskRuleOutcome.Pass, rule.Outcome));
    }

    [Fact]
    public void Stale_market_and_authority_risk_are_hard_rejects()
    {
        var result = MinimalRiskEvaluator.Evaluate(
            Evidence() with
            {
                MarketAsOfTime = Now.AddSeconds(-6),
                FreezeAuthorityEnabled = true
            },
            Policy() with
            {
                MaximumMarketDataAgeSeconds = 5
            });

        Assert.True(result.HardReject);
        Assert.Contains(
            result.RuleResults,
            rule =>
                rule.RuleId == "market-staleness" &&
                rule.Outcome == RiskRuleOutcome.Fail);
        Assert.Contains(
            result.RuleResults,
            rule =>
                rule.RuleId == "freeze-authority" &&
                rule.Outcome == RiskRuleOutcome.Fail);
    }

    [Fact]
    public void Stale_sell_quote_and_excessive_liquidity_drop_are_hard_rejects()
    {
        var evidence = Evidence();
        var result = MinimalRiskEvaluator.Evaluate(
            evidence with
            {
                LiquidityDropBasisPoints = 2_001,
                SellQuote = evidence.SellQuote! with
                {
                    AsOfTime = Now.AddSeconds(-6)
                }
            },
            Policy() with
            {
                MaximumLiquidityDropBasisPoints = 2_000,
                MaximumMarketDataAgeSeconds = 5
            });

        Assert.True(result.HardReject);
        Assert.Contains(
            result.RuleResults,
            rule =>
                rule.RuleId == "sell-quote" &&
                rule.Outcome == RiskRuleOutcome.Fail);
        Assert.Contains(
            result.RuleResults,
            rule =>
                rule.RuleId == "liquidity-drop" &&
                rule.Outcome == RiskRuleOutcome.Fail);
    }

    [Fact]
    public void Candidate_only_becomes_eligible_after_theme_and_risk_pass()
    {
        var theme = ThemeRuleEvaluator.Evaluate(
            "AI",
            "AI",
            Now,
            ThemeRules(hot: ["AI"], required: true));
        var risk = MinimalRiskEvaluator.Evaluate(Evidence(), Policy());

        var result = CandidateEligibilityEvaluator.Evaluate(
            Now.AddSeconds(-60),
            Now,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(300),
            hasUsableLiquidity: true,
            themeMatchRequired: true,
            theme,
            risk,
            maximumAllowedRiskScore: 50);

        Assert.Equal(CandidateEligibilityStatus.Eligible, result.Status);
    }

    [Fact]
    public void Candidate_hard_reject_precedes_observation_completion()
    {
        var theme = ThemeRuleEvaluator.Evaluate(
            "AI",
            "AI",
            Now,
            ThemeRules(hot: ["AI"]));
        var risk = MinimalRiskEvaluator.Evaluate(
            Evidence() with { SellQuote = null },
            Policy());

        var result = CandidateEligibilityEvaluator.Evaluate(
            Now.AddSeconds(-5),
            Now,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(300),
            hasUsableLiquidity: true,
            themeMatchRequired: false,
            theme,
            risk,
            maximumAllowedRiskScore: null);

        Assert.Equal(CandidateEligibilityStatus.Rejected, result.Status);
    }

    private static ThemeRuleDefinition ThemeRules(
        IReadOnlyList<string>? hot = null,
        IReadOnlyList<string>? blocked = null,
        bool required = false) => new(
        hot ?? [],
        blocked ?? [],
        required,
        CaseInsensitive: true,
        NormalizeWhitespace: true,
        ValidUntil: null,
        ConfigurationVersion: "theme-v1");

    private static RiskEvidenceSnapshot Evidence() => new(
        Now,
        Now.AddSeconds(-1),
        QuoteReserveRaw: 10_000,
        EntryPriceImpactBasisPoints: 100,
        LiquidityDropBasisPoints: 0,
        MintAuthorityEnabled: false,
        FreezeAuthorityEnabled: false,
        AdapterAuthorityRisk: false,
        CreatorHoldingBasisPoints: 100,
        Top10HoldingBasisPoints: 1_000,
        PoolVersionSupported: true,
        IsFinalized: true,
        IsReconciled: true,
        SellQuote: new SellQuoteEvidence(
            SellQuoteStatus.Available,
            InputBaseAmount: 100,
            OutputQuoteAmount: 10,
            PriceImpactBasisPoints: 100,
            Now.AddSeconds(-1),
            AdapterVersion: "adapter-v1",
            FailureReason: null));

    private static RiskPolicy Policy() => new(
        RiskModelVersion: "risk-v1",
        FormalRun: false,
        RequireSellQuote: true,
        RejectUnsupportedPoolVersion: true,
        RejectStaleMarketState: true,
        RejectNonFinalizedForFormalRun: true,
        RejectNonReconciledForFormalRun: true,
        RejectMintAuthorityRisk: true,
        RejectFreezeAuthorityRisk: true,
        MinimumQuoteReserveRaw: null,
        MaximumLiquidityDropBasisPoints: null,
        MaximumCreatorHoldingBasisPoints: null,
        MaximumTop10HoldingBasisPoints: null,
        MaximumEntryPriceImpactBasisPoints: 1_000,
        MaximumMarketDataAgeSeconds: 5);
}
