using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Domain.Ingestion;
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
    public async Task Assessment_audit_preserves_retries_and_feeds_acceptance()
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
        var now = DateTimeOffset.Parse("2026-07-28T03:00:00Z");
        var raw = new RawBlockchainEventEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid().ToString("N"),
            Chain = "Solana",
            Network = "mainnet-beta",
            Slot = 456,
            TransactionSignature = $"signature-{Guid.NewGuid():N}",
            InstructionIndex = -1,
            ProgramId = "program",
            EventType = "SolanaTransaction",
            EventOrdinal = 0,
            EventTime = now.AddHours(-2),
            ObservedTime = now.AddHours(-2),
            CommitmentLevel = "finalized",
            CanonicalStatus = CanonicalStatus.Finalized,
            FinalityUpdatedTime = now.AddHours(-2),
            Source = "fallback",
            RawPayload = "{}",
            SchemaVersion = "solana-transaction-v1",
            ProcessingStatus = ProcessingStatus.Completed,
            RetryCount = 1,
            CreatedTime = now.AddHours(-2),
            UpdatedTime = now
        };
        context.RawBlockchainEvents.Add(raw);
        await context.SaveChangesAsync();
        var audit = new PostgresAutomatedAssessmentAuditStore(context);

        await audit.RecordAsync(
            raw.Id,
            "pool",
            456,
            AutomatedAssessmentOutcome.Attempted,
            null,
            now.AddHours(-2),
            CancellationToken.None);
        await audit.RecordAsync(
            raw.Id,
            "pool",
            456,
            AutomatedAssessmentOutcome.Deferred,
            "temporary",
            now.AddHours(-1),
            CancellationToken.None);
        await audit.RecordAsync(
            raw.Id,
            "pool",
            456,
            AutomatedAssessmentOutcome.Attempted,
            null,
            now.AddMinutes(-1),
            CancellationToken.None);
        await audit.RecordAsync(
            raw.Id,
            "pool",
            456,
            AutomatedAssessmentOutcome.Completed,
            "available",
            now,
            CancellationToken.None);
        var facts = await new PostgresM3AcceptanceQuery(context).LoadAsync(
            now.AddHours(-3),
            "fallback",
            CancellationToken.None);

        Assert.Equal(1, facts.Attempted);
        Assert.Equal(1, facts.Completed);
        Assert.Equal(0, facts.Deferred);
        Assert.Equal(1, facts.DeferredOccurrences);
        Assert.Equal(1, facts.RetriedRawEvents);
        Assert.Equal(1, facts.FallbackRawEvents);
        var stored = Assert.Single(context.AutomatedAssessmentAttempts);
        Assert.Equal(2, stored.AttemptCount);
        Assert.Equal(1, stored.DeferredCount);
        await transaction.RollbackAsync();
    }

    [Fact]
    [Trait("Category", "Postgres")]
    public async Task Finality_promotion_requeues_completed_transaction_once()
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
        var raw = new RawBlockchainEventEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid().ToString("N"),
            Chain = "Solana",
            Network = "mainnet-beta",
            Slot = 123,
            TransactionSignature = $"signature-{Guid.NewGuid():N}",
            InstructionIndex = -1,
            ProgramId = "program",
            EventType = "SolanaTransaction",
            EventOrdinal = 0,
            EventTime = now,
            ObservedTime = now,
            CommitmentLevel = "confirmed",
            CanonicalStatus = CanonicalStatus.Confirmed,
            FinalityUpdatedTime = now,
            Source = "test",
            RawPayload = "{}",
            SchemaVersion = "solana-transaction-v1",
            ProcessingStatus = ProcessingStatus.Completed,
            CreatedTime = now,
            UpdatedTime = now
        };
        context.RawBlockchainEvents.Add(raw);
        await context.SaveChangesAsync();
        var store = new PostgresIngestionReconciliationStore(context);

        await store.PromoteSignatureToFinalizedAsync(
            raw.TransactionSignature,
            now.AddMinutes(1),
            CancellationToken.None);

        Assert.Equal(CanonicalStatus.Finalized, raw.CanonicalStatus);
        Assert.Equal(ProcessingStatus.Pending, raw.ProcessingStatus);
        await transaction.RollbackAsync();
    }

    [Fact]
    [Trait("Category", "Postgres")]
    public async Task Automated_context_requires_pool_candidate_and_reconciled_slot()
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
        var baseToken = Token($"base-{Guid.NewGuid():N}", now);
        var quoteToken = Token($"quote-{Guid.NewGuid():N}", now);
        var candidate = new TokenCandidateEntity
        {
            Id = Guid.NewGuid(),
            TokenId = baseToken.Id,
            Status = CandidateStatus.Observing,
            DiscoveredAt = now.AddMinutes(-1),
            UpdatedAt = now
        };
        var programId =
            "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";
        var pool = new LiquidityPoolEntity
        {
            Id = Guid.NewGuid(),
            Chain = "Solana",
            Network = "mainnet-beta",
            PoolAddress = $"pool-{Guid.NewGuid():N}",
            Dex = "Raydium",
            ProgramId = programId,
            BaseTokenId = baseToken.Id,
            QuoteTokenId = quoteToken.Id,
            CreatedSlot = 123,
            CreatedTime = now,
            BaseReserve = 100_000,
            QuoteReserve = 200_000,
            CreatorAddress = "creator",
            AmmConfigAddress = "config",
            LifecycleStatus = PoolLifecycleStatus.Active,
            FirstObservedTime = now,
            UpdatedTime = now
        };
        var checkpoint = new IngestionCheckpointEntity
        {
            Id = Guid.NewGuid(),
            Chain = "Solana",
            Network = "mainnet-beta",
            Source = "test",
            SubscriptionType = programId,
            ObservedThroughSlot = 123,
            PersistedThroughSlot = 123,
            ProcessedThroughSlot = 123,
            FinalizedThroughSlot = 123,
            ReconciledThroughSlot = 123,
            Status = "Healthy",
            UpdatedTime = now
        };
        var slot = new IngestionSlotStateEntity
        {
            Id = Guid.NewGuid(),
            CheckpointId = checkpoint.Id,
            Slot = 123,
            Observed = true,
            Persisted = true,
            Processed = true,
            Finalized = true,
            Reconciled = true,
            HasGap = false,
            UpdatedTime = now
        };
        context.AddRange(
            baseToken,
            quoteToken,
            candidate,
            pool,
            checkpoint,
            slot);
        await context.SaveChangesAsync();

        var source = new PostgresAutomatedAssessmentContextSource(context);
        var result = await source.LoadAsync(
            pool.PoolAddress,
            programId,
            123,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(baseToken.MintAddress, result.TokenAddress);
        Assert.Equal(quoteToken.MintAddress, result.QuoteMint);
        Assert.Equal("creator", result.CreatorAddress);
        Assert.True(result.IsReconciled);
        await transaction.RollbackAsync();
    }

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
            Evidence(now),
            CancellationToken.None);
        var repeated = await store.SaveAsync(
            mint,
            evaluation,
            Evidence(now),
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
            store.SaveAsync(
                mint,
                conflicting,
                Evidence(now),
                CancellationToken.None));

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
                Evidence(now),
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

    private static RiskEvidenceSnapshot Evidence(DateTimeOffset asOfTime) => new(
        asOfTime,
        asOfTime,
        QuoteReserveRaw: 10_000,
        EntryPriceImpactBasisPoints: 100,
        LiquidityDropBasisPoints: null,
        MintAuthorityEnabled: false,
        FreezeAuthorityEnabled: false,
        AdapterAuthorityRisk: false,
        CreatorHoldingBasisPoints: null,
        Top10HoldingBasisPoints: null,
        PoolVersionSupported: true,
        IsFinalized: true,
        IsReconciled: true,
        new SellQuoteEvidence(
            SellQuoteStatus.Available,
            100,
            10,
            100,
            asOfTime,
            "adapter-v1",
            null));

    private static TokenEntity Token(
        string mint,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            Chain = "Solana",
            Network = "mainnet-beta",
            MintAddress = mint,
            LifecycleStatus = TokenLifecycleStatus.Trading,
            CreatedSlot = 123,
            CreatedTime = now,
            FirstObservedTime = now,
            UpdatedTime = now
        };
}
