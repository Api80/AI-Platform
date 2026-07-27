using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;

namespace CryptoIntelligence.Application.Intelligence;

public enum AutomatedAssessmentOutcome
{
    Attempted,
    Deferred,
    Unsupported,
    Completed
}

public interface IAutomatedAssessmentAuditStore
{
    Task RecordAsync(
        Guid rawEventId,
        string poolAddress,
        ulong slot,
        AutomatedAssessmentOutcome outcome,
        string? reason,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}

public sealed record M3AcceptanceFacts(
    DateTimeOffset? FirstObservedAt,
    DateTimeOffset? LastObservedAt,
    long Attempted,
    long Completed,
    long Deferred,
    long Unsupported,
    long DeferredOccurrences,
    long EvidenceRecords,
    long HardRejects,
    long RetriedRawEvents,
    long DeadLetterRawEvents,
    long UnresolvedGaps,
    long FallbackRawEvents,
    IReadOnlyList<IngestionCheckpointSnapshot> Checkpoints);

public interface IM3AcceptanceQuery
{
    Task<M3AcceptanceFacts> LoadAsync(
        DateTimeOffset from,
        string? fallbackSourceName,
        CancellationToken cancellationToken);
}

public sealed record M3AcceptanceReport(
    DateTimeOffset GeneratedAt,
    DateTimeOffset RequestedFrom,
    DateTimeOffset? FirstObservedAt,
    DateTimeOffset? LastObservedAt,
    decimal ObservedRunHours,
    int RequiredRunHours,
    bool FormalRun,
    long Attempted,
    long Completed,
    long Deferred,
    long Unsupported,
    long DeferredOccurrences,
    int TerminalCoverageBasisPoints,
    long EvidenceRecords,
    long HardRejects,
    long RetriedRawEvents,
    long DeadLetterRawEvents,
    long UnresolvedGaps,
    long FallbackRawEvents,
    IReadOnlyList<IngestionCheckpointSnapshot> Checkpoints,
    IReadOnlyList<string> BlockingReasons)
{
    public bool AutomatedChecksPassed => BlockingReasons.Count == 0;
}

public sealed class M3AcceptanceService(
    IM3AcceptanceQuery query,
    MvpConfiguration configuration)
{
    public async Task<M3AcceptanceReport> EvaluateAsync(
        DateTimeOffset from,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (from >= now)
        {
            throw new ArgumentOutOfRangeException(
                nameof(from),
                "Acceptance window must start before the report time.");
        }

        var facts = await query.LoadAsync(
            from,
            configuration.Source.FallbackRpcSourceName,
            cancellationToken);
        var hours = facts.FirstObservedAt is not null &&
                    facts.LastObservedAt is not null
            ? Math.Max(
                0,
                (decimal)(facts.LastObservedAt.Value -
                          facts.FirstObservedAt.Value).TotalHours)
            : 0;
        var terminal = facts.Completed + facts.Unsupported;
        var coverage = facts.Attempted == 0
            ? 0
            : (int)Math.Min(
                10_000m,
                terminal * 10_000m / facts.Attempted);
        var blockers = BuildBlockers(facts, hours, coverage);
        return new M3AcceptanceReport(
            now,
            from,
            facts.FirstObservedAt,
            facts.LastObservedAt,
            decimal.Round(hours, 2),
            configuration.Acceptance.MinimumRunHours,
            configuration.FormalRun,
            facts.Attempted,
            facts.Completed,
            facts.Deferred,
            facts.Unsupported,
            facts.DeferredOccurrences,
            coverage,
            facts.EvidenceRecords,
            facts.HardRejects,
            facts.RetriedRawEvents,
            facts.DeadLetterRawEvents,
            facts.UnresolvedGaps,
            facts.FallbackRawEvents,
            facts.Checkpoints,
            blockers);
    }

    private IReadOnlyList<string> BuildBlockers(
        M3AcceptanceFacts facts,
        decimal observedRunHours,
        int coverage)
    {
        var values = new List<string>();
        if (!configuration.FormalRun)
        {
            values.Add("The service is not running in formal mode.");
        }

        if (observedRunHours < configuration.Acceptance.MinimumRunHours)
        {
            values.Add(
                $"Observed run duration is below {configuration.Acceptance.MinimumRunHours} hours.");
        }

        if (facts.Attempted <
            configuration.Acceptance.MinimumAutomatedAssessmentAttempts)
        {
            values.Add("Automated assessment sample size is below the configured minimum.");
        }

        if (coverage <
            configuration.Acceptance.MinimumTerminalCoverageBasisPoints)
        {
            values.Add("Terminal assessment coverage is below the configured threshold.");
        }

        if (facts.UnresolvedGaps > 0)
        {
            values.Add("Unresolved ingestion gaps remain.");
        }

        if (facts.DeadLetterRawEvents > 0)
        {
            values.Add("Dead-letter raw events remain in the acceptance window.");
        }

        if (configuration.Acceptance.RequireFallbackExercise &&
            facts.FallbackRawEvents == 0)
        {
            values.Add("The fallback RPC has not been exercised.");
        }

        var expectedPrograms = configuration.Source.ProgramIds.ToHashSet(
            StringComparer.Ordinal);
        var checkpoints = facts.Checkpoints
            .Where(value => expectedPrograms.Contains(value.Key.SubscriptionType))
            .ToArray();
        if (checkpoints.Length != expectedPrograms.Count)
        {
            values.Add("Not every configured program has an ingestion checkpoint.");
        }

        if (checkpoints.Any(value =>
                value.Watermarks.PersistedThroughSlot >
                    value.Watermarks.ObservedThroughSlot ||
                value.Watermarks.ProcessedThroughSlot >
                    value.Watermarks.PersistedThroughSlot ||
                value.Watermarks.FinalizedThroughSlot >
                    value.Watermarks.ProcessedThroughSlot ||
                value.Watermarks.ReconciledThroughSlot >
                    value.Watermarks.FinalizedThroughSlot))
        {
            values.Add("At least one checkpoint violates watermark ordering.");
        }

        return values;
    }
}
