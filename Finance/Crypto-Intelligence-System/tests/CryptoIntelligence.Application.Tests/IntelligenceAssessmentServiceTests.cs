using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Application.Tests;

public sealed class IntelligenceAssessmentServiceTests
{
    [Fact]
    public async Task Coordinator_evaluates_then_persists_one_result()
    {
        var now = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        var store = new RecordingStore();
        var service = new IntelligenceAssessmentService(
            new IntelligenceEvaluationService(Configuration()),
            store);
        var input = new IntelligenceEvaluationInput(
            "Example AI",
            "EAI",
            now.AddMinutes(-1),
            now,
            HasUsableLiquidity: true,
            Evidence(now));

        var result = await service.EvaluateAndSaveAsync(
            "mint",
            input,
            CancellationToken.None);

        Assert.Equal("mint", store.TokenAddress);
        Assert.Same(result.Evaluation, store.Evaluation);
        Assert.Equal(CandidateEligibilityStatus.Eligible, result.Evaluation.Candidate.Status);
    }

    private static RiskEvidenceSnapshot Evidence(DateTimeOffset now) => new(
        now,
        now.AddSeconds(-1),
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
        new SellQuoteEvidence(
            SellQuoteStatus.Available,
            InputBaseAmount: 100,
            OutputQuoteAmount: 10,
            PriceImpactBasisPoints: 100,
            now.AddSeconds(-1),
            "adapter-v1",
            null));

    private static MvpConfiguration Configuration() => new()
    {
        Radar = new RadarConfiguration
        {
            MinimumObservationSeconds = 30,
            MaximumEntryAgeSeconds = 300
        },
        Theme = new ThemeConfiguration
        {
            HotKeywords = ["AI"],
            ConfigurationVersion = "theme-v1"
        },
        Risk = new RiskConfiguration
        {
            ModelVersion = "risk-v1",
            HardReject = new HardRejectConfiguration
            {
                MaximumEntryPriceImpactBasisPoints = 1_000,
                MaximumMarketDataAgeSeconds = 5
            }
        }
    };

    private sealed class RecordingStore : IIntelligenceAssessmentStore
    {
        public string? TokenAddress { get; private set; }
        public IntelligenceEvaluationResult? Evaluation { get; private set; }

        public Task<StoredIntelligenceEvaluation> SaveAsync(
            string tokenAddress,
            IntelligenceEvaluationResult evaluation,
            CancellationToken cancellationToken)
        {
            TokenAddress = tokenAddress;
            Evaluation = evaluation;
            return Task.FromResult(new StoredIntelligenceEvaluation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ThemeCreated: true,
                RiskCreated: true));
        }
    }
}
