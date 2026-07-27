using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Domain.Ingestion;

namespace CryptoIntelligence.Application.Tests;

public sealed class M3AcceptanceServiceTests
{
    [Fact]
    public async Task Formal_healthy_window_passes()
    {
        var now = DateTimeOffset.Parse("2026-07-28T08:00:00Z");
        var configuration = Configuration(formalRun: true);
        var checkpoint = new IngestionCheckpointSnapshot(
            Guid.NewGuid(),
            new IngestionCheckpointKey(
                "Solana",
                "mainnet-beta",
                "primary",
                "program"),
            new IngestionWatermarks(100, 100, 100, 100, 100),
            "Healthy",
            now);
        var service = new M3AcceptanceService(
            new StubQuery(new M3AcceptanceFacts(
                now.AddHours(-2),
                now,
                Attempted: 10,
                Completed: 9,
                Deferred: 0,
                Unsupported: 1,
                DeferredOccurrences: 2,
                EvidenceRecords: 10,
                HardRejects: 1,
                RetriedRawEvents: 2,
                DeadLetterRawEvents: 0,
                UnresolvedGaps: 0,
                FallbackRawEvents: 1,
                [checkpoint])),
            configuration);

        var report = await service.EvaluateAsync(
            now.AddHours(-3),
            now,
            CancellationToken.None);

        Assert.True(report.AutomatedChecksPassed);
        Assert.Equal(10_000, report.TerminalCoverageBasisPoints);
        Assert.Empty(report.BlockingReasons);
    }

    [Fact]
    public async Task Development_empty_window_fails_closed()
    {
        var now = DateTimeOffset.Parse("2026-07-28T08:00:00Z");
        var service = new M3AcceptanceService(
            new StubQuery(new M3AcceptanceFacts(
                null,
                null,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                0,
                [])),
            Configuration(formalRun: false));

        var report = await service.EvaluateAsync(
            now.AddHours(-3),
            now,
            CancellationToken.None);

        Assert.False(report.AutomatedChecksPassed);
        Assert.Contains(
            report.BlockingReasons,
            value => value.Contains("not running in formal mode"));
        Assert.Contains(
            report.BlockingReasons,
            value => value.Contains("Unresolved ingestion gaps"));
        Assert.Contains(
            report.BlockingReasons,
            value => value.Contains("Dead-letter"));
    }

    private static MvpConfiguration Configuration(bool formalRun) => new()
    {
        FormalRun = formalRun,
        Source = new SourceConfiguration
        {
            ProgramIds = ["program"],
            RpcSourceName = "primary",
            FallbackRpcSourceName = "fallback"
        },
        Acceptance = new M3AcceptanceConfiguration
        {
            MinimumRunHours = 1,
            MinimumAutomatedAssessmentAttempts = 1,
            MinimumTerminalCoverageBasisPoints = 9_500,
            RequireFallbackExercise = true
        }
    };

    private sealed class StubQuery(M3AcceptanceFacts facts)
        : IM3AcceptanceQuery
    {
        public Task<M3AcceptanceFacts> LoadAsync(
            DateTimeOffset from,
            string? fallbackSourceName,
            CancellationToken cancellationToken) =>
            Task.FromResult(facts);
    }
}
