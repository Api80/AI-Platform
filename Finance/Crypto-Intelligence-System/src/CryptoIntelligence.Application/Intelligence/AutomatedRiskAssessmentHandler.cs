using System.Diagnostics.Metrics;
using System.Globalization;
using System.Numerics;
using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Domain.Ingestion;
using CryptoIntelligence.Domain.Intelligence;

namespace CryptoIntelligence.Application.Intelligence;

public sealed record AutomatedAssessmentContext(
    string TokenAddress,
    string? TokenName,
    string? Symbol,
    string? CreatorAddress,
    DateTimeOffset DiscoveredAt,
    string PoolProgramId,
    string BaseMint,
    string QuoteMint,
    string? AmmConfigAddress,
    bool IsReconciled);

public interface IAutomatedAssessmentContextSource
{
    Task<AutomatedAssessmentContext?> LoadAsync(
        string poolAddress,
        string programId,
        ulong slot,
        CancellationToken cancellationToken);
}

public sealed class RiskEvidenceTemporarilyUnavailableException(string message)
    : Exception(message);

public sealed class AutomatedRiskAssessmentHandler(
    IAutomatedAssessmentContextSource contexts,
    RiskEvidenceCollector evidenceCollector,
    IntelligenceAssessmentService assessments,
    MvpConfiguration configuration)
    : IProjectionEventHandler
{
    private const string CpmmProgramId =
        "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C";

    private static readonly Meter Meter = new(
        "CryptoIntelligence.AutomatedRiskAssessment",
        "1.0");

    private static readonly Counter<long> Attempted =
        Meter.CreateCounter<long>("crypto.risk_assessment.attempted");

    private static readonly Counter<long> Completed =
        Meter.CreateCounter<long>("crypto.risk_assessment.completed");

    private static readonly Counter<long> Deferred =
        Meter.CreateCounter<long>("crypto.risk_assessment.deferred");

    private static readonly Counter<long> Unsupported =
        Meter.CreateCounter<long>("crypto.risk_assessment.unsupported");

    public async Task HandleAsync(
        ProjectionEvent projectionEvent,
        CancellationToken cancellationToken)
    {
        if (projectionEvent.Event.DomainEventType != "SwapObserved" ||
            projectionEvent.Event.ProgramId != CpmmProgramId ||
            projectionEvent.CanonicalStatus != CanonicalStatus.Finalized)
        {
            return;
        }

        var attributes = projectionEvent.Event.Attributes;
        if (attributes is null ||
            !TryGet(attributes, "pool_address", out var poolAddress))
        {
            Unsupported.Add(1, Tag("missing-pool"));
            return;
        }

        Attempted.Add(1);
        var context = await contexts.LoadAsync(
            poolAddress,
            projectionEvent.Event.ProgramId,
            projectionEvent.Slot,
            cancellationToken);
        if (context is null)
        {
            throw DeferredException("Pool/candidate projection is not available.");
        }

        if (configuration.FormalRun && !context.IsReconciled)
        {
            throw DeferredException(
                "The finalized slot has not reached the reconciled watermark.");
        }

        if (!TryBuildSnapshot(
                projectionEvent,
                context,
                configuration,
                out var snapshot,
                out var sellAmount,
                out var entryImpact,
                out var reason))
        {
            Unsupported.Add(1, Tag(reason));
            return;
        }

        if (reason != "available")
        {
            Unsupported.Add(1, Tag(reason));
        }

        var collected = await evidenceCollector.CollectAsync(
            new RiskEvidenceCollectionInput(
                context.TokenAddress,
                context.CreatorAddress,
                snapshot,
                sellAmount,
                projectionEvent.EventTime,
                snapshot.OutputReserveRaw <= new BigInteger(decimal.MaxValue)
                    ? (decimal)snapshot.OutputReserveRaw
                    : null,
                entryImpact,
                LiquidityDropBasisPoints: null,
                AdapterAuthorityRisk: false,
                IsFinalized: true,
                context.IsReconciled),
            cancellationToken);
        if (collected.Authority.Availability ==
            EvidenceAvailability.TemporarilyUnavailable ||
            collected.Holders.Availability ==
            EvidenceAvailability.TemporarilyUnavailable)
        {
            throw DeferredException(
                "Solana authority or holder evidence is temporarily unavailable.");
        }

        await assessments.EvaluateAndSaveAsync(
            context.TokenAddress,
            new IntelligenceEvaluationInput(
                context.TokenName,
                context.Symbol,
                context.DiscoveredAt,
                projectionEvent.EventTime,
                HasUsableLiquidity:
                    snapshot.InputReserveRaw > 0 &&
                    snapshot.OutputReserveRaw > 0,
                collected.Snapshot),
            cancellationToken);
        Completed.Add(1);
    }

    private static bool TryBuildSnapshot(
        ProjectionEvent value,
        AutomatedAssessmentContext context,
        MvpConfiguration configuration,
        out RaydiumCpmmPoolSnapshot snapshot,
        out BigInteger sellAmount,
        out int? entryImpact,
        out string reason)
    {
        var attributes = value.Event.Attributes!;
        snapshot = default!;
        sellAmount = BigInteger.Zero;
        entryImpact = null;
        reason = "incomplete-same-slot-evidence";
        if (!TryGet(attributes, "input_mint", out var inputMint) ||
            !TryGet(attributes, "output_mint", out var outputMint) ||
            !BigInteger.TryParse(
                attributes.GetValueOrDefault("input_vault_before"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var inputReserve) ||
            !BigInteger.TryParse(
                attributes.GetValueOrDefault("output_vault_before"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var outputReserve) ||
            inputReserve <= 0 ||
            outputReserve <= 0 ||
            !TryGet(
                attributes,
                "input_token_program_id",
                out var inputTokenProgram) ||
            !TryGet(
                attributes,
                "output_token_program_id",
                out var outputTokenProgram))
        {
            return false;
        }

        BigInteger baseReserve;
        BigInteger quoteReserve;
        string baseTokenProgram;
        string quoteTokenProgram;
        if (inputMint == context.BaseMint &&
            outputMint == context.QuoteMint)
        {
            baseReserve = inputReserve;
            quoteReserve = outputReserve;
            baseTokenProgram = inputTokenProgram;
            quoteTokenProgram = outputTokenProgram;
        }
        else if (inputMint == context.QuoteMint &&
                 outputMint == context.BaseMint)
        {
            baseReserve = outputReserve;
            quoteReserve = inputReserve;
            baseTokenProgram = outputTokenProgram;
            quoteTokenProgram = inputTokenProgram;
        }
        else
        {
            reason = "pool-mint-orientation-mismatch";
            return false;
        }

        var feeSupported = bool.TryParse(
            attributes.GetValueOrDefault("fee_evidence_supported"),
            out var supported) && supported;
        var tradingFee = Int(attributes, "trading_fee_bps");
        var creatorFee = Int(attributes, "creator_fee_bps");
        var adapterVersion = feeSupported
            ? configuration.Source.AdapterVersion
            : $"{configuration.Source.AdapterVersion}-unsupported-fees";
        snapshot = new RaydiumCpmmPoolSnapshot(
            attributes["pool_address"],
            value.Event.ProgramId,
            adapterVersion,
            context.BaseMint,
            context.QuoteMint,
            baseTokenProgram,
            quoteTokenProgram,
            baseReserve,
            quoteReserve,
            tradingFee,
            creatorFee,
            value.Slot,
            value.EventTime);
        sellAmount = BigInteger.Max(
            BigInteger.One,
            baseReserve *
            configuration.Risk.SellQuoteProbeReserveBasisPoints /
            10_000);
        if (feeSupported)
        {
            var entryAmount = BigInteger.Max(
                BigInteger.One,
                quoteReserve *
                configuration.Risk.SellQuoteProbeReserveBasisPoints /
                10_000);
            entryImpact = CpmmExactInputQuoteCalculator.Calculate(
                    quoteReserve,
                    baseReserve,
                    entryAmount,
                    tradingFee,
                    creatorFee)
                .TotalImpactBasisPoints;
        }

        reason = feeSupported ? "available" : "unsupported-fee-evidence";
        return true;
    }

    private RiskEvidenceTemporarilyUnavailableException DeferredException(
        string message)
    {
        Deferred.Add(1);
        return new RiskEvidenceTemporarilyUnavailableException(message);
    }

    private static KeyValuePair<string, object?> Tag(string reason) =>
        new("reason", reason);

    private static bool TryGet(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out string value)
    {
        value = attributes.GetValueOrDefault(key) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static int Int(
        IReadOnlyDictionary<string, string> attributes,
        string key) =>
        int.TryParse(
            attributes.GetValueOrDefault(key),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
}
