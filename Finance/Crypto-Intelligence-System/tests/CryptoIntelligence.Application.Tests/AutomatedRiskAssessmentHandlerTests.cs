using System.Numerics;
using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Domain.Ingestion;
using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Application.Tests;

public sealed class AutomatedRiskAssessmentHandlerTests
{
    private const string CpmmProgramId =
        "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";

    private const string TokenProgramId =
        "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    [Fact]
    public async Task Finalized_swap_orients_same_slot_evidence_and_persists()
    {
        var now = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        var quote = new RecordingQuoteSource();
        var store = new RecordingAssessmentStore();
        var audit = new RecordingAuditStore();
        var configuration = Configuration();
        var handler = new AutomatedRiskAssessmentHandler(
            new StubContextSource(new AutomatedAssessmentContext(
                "base",
                "Example AI",
                "EAI",
                "creator",
                now.AddMinutes(-1),
                CpmmProgramId,
                "base",
                "quote",
                "config",
                IsReconciled: true)),
            audit,
            new RiskEvidenceCollector(quote, new StubTokenEvidenceSource(now)),
            new IntelligenceAssessmentService(
                new IntelligenceEvaluationService(configuration),
                store),
            configuration);

        await handler.HandleAsync(
            Projection(now, CanonicalStatus.Finalized),
            CancellationToken.None);

        Assert.NotNull(quote.Snapshot);
        Assert.Equal(new BigInteger(100_000), quote.Snapshot.InputReserveRaw);
        Assert.Equal(new BigInteger(200_000), quote.Snapshot.OutputReserveRaw);
        Assert.Equal(new BigInteger(1_000), quote.AmountIn);
        Assert.NotNull(store.Evaluation);
        Assert.NotNull(store.Evidence);
        Assert.Equal(
            CandidateEligibilityStatus.Eligible,
            store.Evaluation.Candidate.Status);
        Assert.True(store.Evidence.IsFinalized);
        Assert.True(store.Evidence.IsReconciled);
        Assert.Equal(
            [
                AutomatedAssessmentOutcome.Attempted,
                AutomatedAssessmentOutcome.Completed
            ],
            audit.Outcomes);
    }

    [Fact]
    public async Task Confirmed_swap_is_not_assessed()
    {
        var now = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        var store = new RecordingAssessmentStore();
        var audit = new RecordingAuditStore();
        var configuration = Configuration();
        var handler = new AutomatedRiskAssessmentHandler(
            new StubContextSource(null),
            audit,
            new RiskEvidenceCollector(
                new RecordingQuoteSource(),
                new StubTokenEvidenceSource(now)),
            new IntelligenceAssessmentService(
                new IntelligenceEvaluationService(configuration),
                store),
            configuration);

        await handler.HandleAsync(
            Projection(now, CanonicalStatus.Confirmed),
            CancellationToken.None);

        Assert.Null(store.Evaluation);
        Assert.Empty(audit.Outcomes);
    }

    private static ProjectionEvent Projection(
        DateTimeOffset now,
        CanonicalStatus status) => new(
        Guid.NewGuid(),
        123,
        now,
        now,
        new ParsedAdapterEvent(
            CpmmProgramId,
            "swap_base_input",
            0,
            null,
            0,
            "SwapObserved",
            "InstructionDerived",
            "fingerprint",
            new Dictionary<string, string>
            {
                ["pool_address"] = "pool",
                ["input_mint"] = "quote",
                ["output_mint"] = "base",
                ["input_vault_before"] = "200000",
                ["output_vault_before"] = "100000",
                ["input_token_program_id"] = TokenProgramId,
                ["output_token_program_id"] = TokenProgramId,
                ["fee_evidence_supported"] = "true",
                ["trading_fee_bps"] = "25",
                ["creator_fee_bps"] = "0"
            }),
        status);

    private static MvpConfiguration Configuration() => new()
    {
        FormalRun = false,
        Source = new SourceConfiguration
        {
            AdapterVersion = "adapter-v1"
        },
        Radar = new RadarConfiguration
        {
            MinimumObservationSeconds = 30,
            MaximumEntryAgeSeconds = 300
        },
        Theme = new ThemeConfiguration
        {
            ConfigurationVersion = "theme-v1"
        },
        Risk = new RiskConfiguration
        {
            ModelVersion = "risk-v1",
            SellQuoteProbeReserveBasisPoints = 100,
            HardReject = new HardRejectConfiguration
            {
                MaximumEntryPriceImpactBasisPoints = 1_000,
                MaximumMarketDataAgeSeconds = 5
            }
        }
    };

    private sealed class StubContextSource(AutomatedAssessmentContext? value)
        : IAutomatedAssessmentContextSource
    {
        public Task<AutomatedAssessmentContext?> LoadAsync(
            string poolAddress,
            string programId,
            ulong slot,
            CancellationToken cancellationToken) =>
            Task.FromResult(value);
    }

    private sealed class RecordingQuoteSource : IRaydiumSellQuoteEvidenceSource
    {
        public RaydiumCpmmPoolSnapshot? Snapshot { get; private set; }
        public BigInteger AmountIn { get; private set; }

        public SellQuoteEvidence QuoteExactInput(
            RaydiumCpmmPoolSnapshot snapshot,
            BigInteger amountInRaw,
            DateTimeOffset evaluatedAt)
        {
            Snapshot = snapshot;
            AmountIn = amountInRaw;
            return new SellQuoteEvidence(
                SellQuoteStatus.Available,
                (decimal)amountInRaw,
                OutputQuoteAmount: 1,
                PriceImpactBasisPoints: 100,
                snapshot.AsOfTime,
                snapshot.AdapterVersion,
                null);
        }
    }

    private sealed class StubTokenEvidenceSource(DateTimeOffset now)
        : ISolanaTokenRiskEvidenceSource
    {
        public Task<TokenAuthorityEvidence> GetAuthorityAsync(
            string mintAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new TokenAuthorityEvidence(
                EvidenceAvailability.Available,
                mintAddress,
                MintAuthorityEnabled: false,
                FreezeAuthorityEnabled: false,
                null,
                null,
                TokenProgramId,
                123,
                now,
                null));

        public Task<HolderConcentrationEvidence> GetHolderConcentrationAsync(
            string mintAddress,
            string? creatorAddress,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HolderConcentrationEvidence(
                EvidenceAvailability.Available,
                mintAddress,
                creatorAddress,
                100_000,
                1_000,
                10_000,
                100,
                1_000,
                123,
                now,
                null));
    }

    private sealed class RecordingAssessmentStore : IIntelligenceAssessmentStore
    {
        public IntelligenceEvaluationResult? Evaluation { get; private set; }
        public RiskEvidenceSnapshot? Evidence { get; private set; }

        public Task<StoredIntelligenceEvaluation> SaveAsync(
            string tokenAddress,
            IntelligenceEvaluationResult evaluation,
            RiskEvidenceSnapshot evidence,
            CancellationToken cancellationToken)
        {
            Evaluation = evaluation;
            Evidence = evidence;
            return Task.FromResult(new StoredIntelligenceEvaluation(
                Guid.NewGuid(),
                Guid.NewGuid(),
                true,
                true));
        }
    }

    private sealed class RecordingAuditStore : IAutomatedAssessmentAuditStore
    {
        public List<AutomatedAssessmentOutcome> Outcomes { get; } = [];

        public Task RecordAsync(
            Guid rawEventId,
            string poolAddress,
            ulong slot,
            AutomatedAssessmentOutcome outcome,
            string? reason,
            DateTimeOffset timestamp,
            CancellationToken cancellationToken)
        {
            Outcomes.Add(outcome);
            return Task.CompletedTask;
        }
    }
}
