using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresM3AcceptanceQuery(
    CryptoIntelligenceDbContext context)
    : IM3AcceptanceQuery
{
    public async Task<M3AcceptanceFacts> LoadAsync(
        DateTimeOffset from,
        string? fallbackSourceName,
        CancellationToken cancellationToken)
    {
        var raw = context.RawBlockchainEvents
            .AsNoTracking()
            .Where(value => value.CreatedTime >= from);
        var firstObserved = await raw.MinAsync(
            value => (DateTimeOffset?)value.ObservedTime,
            cancellationToken);
        var lastObserved = await raw.MaxAsync(
            value => (DateTimeOffset?)value.ObservedTime,
            cancellationToken);
        var attempts = context.AutomatedAssessmentAttempts
            .AsNoTracking()
            .Where(value => value.FirstAttemptTime >= from);
        var attempted = await attempts.LongCountAsync(cancellationToken);
        var completed = await attempts.LongCountAsync(
            value => value.Outcome == AutomatedAssessmentOutcome.Completed,
            cancellationToken);
        var deferred = await attempts.LongCountAsync(
            value => value.Outcome == AutomatedAssessmentOutcome.Deferred,
            cancellationToken);
        var unsupported = await attempts.LongCountAsync(
            value => value.Outcome == AutomatedAssessmentOutcome.Unsupported,
            cancellationToken);
        var deferredOccurrences = await attempts.SumAsync(
            value => (long)value.DeferredCount,
            cancellationToken);
        var evidenceRecords = await context.RiskAssessments
            .AsNoTracking()
            .LongCountAsync(
                value => value.CreatedTime >= from && value.Evidence != null,
                cancellationToken);
        var hardRejects = await context.RiskAssessments
            .AsNoTracking()
            .LongCountAsync(
                value => value.CreatedTime >= from &&
                         value.Evidence != null &&
                         value.HardReject,
                cancellationToken);
        var retried = await raw.LongCountAsync(
            value => value.RetryCount > 0,
            cancellationToken);
        var deadLetters = await raw.LongCountAsync(
            value => value.ProcessingStatus == ProcessingStatus.DeadLetter,
            cancellationToken);
        var gaps = await context.IngestionSlotStates
            .AsNoTracking()
            .LongCountAsync(value => value.HasGap, cancellationToken);
        var fallbackEvents = string.IsNullOrWhiteSpace(fallbackSourceName)
            ? 0
            : await raw.LongCountAsync(
                value => value.Source == fallbackSourceName,
                cancellationToken);
        var checkpoints = await new PostgresIngestionReconciliationStore(context)
            .ListCheckpointsAsync(cancellationToken);
        return new M3AcceptanceFacts(
            firstObserved,
            lastObserved,
            attempted,
            completed,
            deferred,
            unsupported,
            deferredOccurrences,
            evidenceRecords,
            hardRejects,
            retried,
            deadLetters,
            gaps,
            fallbackEvents,
            checkpoints);
    }
}
