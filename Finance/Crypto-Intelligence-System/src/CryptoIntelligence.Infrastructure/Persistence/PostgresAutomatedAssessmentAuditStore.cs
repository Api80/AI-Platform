using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresAutomatedAssessmentAuditStore(
    CryptoIntelligenceDbContext context)
    : IAutomatedAssessmentAuditStore
{
    public async Task RecordAsync(
        Guid rawEventId,
        string poolAddress,
        ulong slot,
        AutomatedAssessmentOutcome outcome,
        string? reason,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolAddress);
        var value = await context.AutomatedAssessmentAttempts
            .SingleOrDefaultAsync(
                candidate => candidate.RawEventId == rawEventId,
                cancellationToken);
        if (value is null)
        {
            value = new AutomatedAssessmentAttemptEntity
            {
                Id = Guid.NewGuid(),
                RawEventId = rawEventId,
                PoolAddress = poolAddress,
                Slot = checked((long)slot),
                Outcome = outcome,
                Reason = Trim(reason),
                AttemptCount =
                    outcome == AutomatedAssessmentOutcome.Attempted ? 1 : 0,
                DeferredCount =
                    outcome == AutomatedAssessmentOutcome.Deferred ? 1 : 0,
                FirstAttemptTime = timestamp,
                LastAttemptTime = timestamp,
                CompletedTime = IsTerminal(outcome) ? timestamp : null
            };
            context.AutomatedAssessmentAttempts.Add(value);
        }
        else
        {
            value.PoolAddress = poolAddress;
            value.Slot = checked((long)slot);
            value.Outcome = outcome;
            value.Reason = Trim(reason);
            value.LastAttemptTime = timestamp;
            if (outcome == AutomatedAssessmentOutcome.Attempted)
            {
                value.AttemptCount++;
            }

            if (outcome == AutomatedAssessmentOutcome.Deferred)
            {
                value.DeferredCount++;
            }

            if (IsTerminal(outcome))
            {
                value.CompletedTime = timestamp;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsTerminal(AutomatedAssessmentOutcome outcome) =>
        outcome is AutomatedAssessmentOutcome.Completed or
            AutomatedAssessmentOutcome.Unsupported;

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= 1_000
                ? value
                : value[..1_000];
}
