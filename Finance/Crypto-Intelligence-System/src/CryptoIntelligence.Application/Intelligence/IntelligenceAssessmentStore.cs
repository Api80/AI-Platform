using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Application.Intelligence;

public sealed record StoredIntelligenceEvaluation(
    Guid ThemeMatchId,
    Guid RiskAssessmentId,
    bool ThemeCreated,
    bool RiskCreated);

public interface IIntelligenceAssessmentStore
{
    Task<StoredIntelligenceEvaluation> SaveAsync(
        string tokenAddress,
        IntelligenceEvaluationResult evaluation,
        RiskEvidenceSnapshot evidence,
        CancellationToken cancellationToken);
}

public sealed record PersistedIntelligenceEvaluation(
    IntelligenceEvaluationResult Evaluation,
    StoredIntelligenceEvaluation Stored);

public sealed class IntelligenceAssessmentService(
    IntelligenceEvaluationService evaluator,
    IIntelligenceAssessmentStore store)
{
    public async Task<PersistedIntelligenceEvaluation> EvaluateAndSaveAsync(
        string tokenAddress,
        IntelligenceEvaluationInput input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenAddress);
        var evaluation = evaluator.Evaluate(input);
        var stored = await store.SaveAsync(
            tokenAddress,
            evaluation,
            input.RiskEvidence,
            cancellationToken);
        return new PersistedIntelligenceEvaluation(evaluation, stored);
    }
}
