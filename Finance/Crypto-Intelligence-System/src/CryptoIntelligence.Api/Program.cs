using CryptoIntelligence.Api;
using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Contracts;
using CryptoIntelligence.Domain.Intelligence;
using CryptoIntelligence.Domain.Radar;
using CryptoIntelligence.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ");

var mvpConfiguration = builder.Configuration
    .GetSection(MvpConfiguration.SectionName)
    .Get<MvpConfiguration>();
MvpConfigurationValidator.ThrowIfInvalid(mvpConfiguration);
var configurationSnapshot = ConfigurationSnapshotFactory.Create(
    mvpConfiguration!,
    DateTimeOffset.UtcNow);
builder.Services.AddSingleton(mvpConfiguration!);
builder.Services.AddSingleton(configurationSnapshot);
builder.Services.AddSingleton<IntelligenceEvaluationService>();
builder.Services.AddScoped<IntelligenceAssessmentService>();

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("CRYPTO_DB_CONNECTION")
    ?? throw new InvalidOperationException(
        "PostgreSQL connection must be supplied through ConnectionStrings__Postgres " +
        "or CRYPTO_DB_CONNECTION.");
builder.Services.AddCryptoIntelligencePersistence(connectionString);
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<CryptoIntelligenceDbContext>(
        "postgres",
        tags: ["ready"]);

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapGet(
    "/api/v1/system/status",
    (ConfigurationSnapshot snapshot) => new SystemStatusResponse(
        "Crypto Intelligence API",
        "M3 Theme and Minimal Risk",
        snapshot.ConfigurationVersion,
        snapshot.ConfigurationHash,
        DateTimeOffset.UtcNow));

app.MapGet(
    "/api/v1/radar/candidates",
    async (
        string? status,
        int? limit,
        IRadarQueryService query,
        CancellationToken cancellationToken) =>
    {
        CandidateStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<CandidateStatus>(
                    status,
                    ignoreCase: true,
                    out var candidateStatus))
            {
                return Results.BadRequest(new
                {
                    error = $"Unknown candidate status '{status}'."
                });
            }

            parsedStatus = candidateStatus;
        }

        var candidates = await query.ListCandidatesAsync(
            parsedStatus,
            limit ?? 50,
            cancellationToken);
        return Results.Ok(candidates.Select(ToResponse));
    });

app.MapGet(
    "/api/v1/radar/candidates/{tokenAddress}",
    async (
        string tokenAddress,
        IRadarQueryService query,
        CancellationToken cancellationToken) =>
    {
        var candidate = await query.FindCandidateAsync(
            tokenAddress,
            cancellationToken);
        return candidate is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(candidate));
    });

app.MapGet(
    "/api/v1/ingestion/checkpoints",
    async (
        IIngestionReconciliationStore store,
        CancellationToken cancellationToken) =>
    {
        var checkpoints = await store.ListCheckpointsAsync(cancellationToken);
        return Results.Ok(checkpoints.Select(value =>
            new IngestionCheckpointResponse(
                value.Key.Source,
                value.Key.SubscriptionType,
                value.Watermarks.ObservedThroughSlot,
                value.Watermarks.PersistedThroughSlot,
                value.Watermarks.ProcessedThroughSlot,
                value.Watermarks.FinalizedThroughSlot,
                value.Watermarks.ReconciledThroughSlot,
                value.Status,
                value.UpdatedTime)));
    });

app.MapGet(
    "/api/v1/ingestion/gaps",
    async (
        int? limit,
        IIngestionReconciliationStore store,
        CancellationToken cancellationToken) =>
    {
        var gaps = await store.ListGapsAsync(limit ?? 100, cancellationToken);
        return Results.Ok(gaps.Select(value => new IngestionGapResponse(
            value.SubscriptionType,
            value.Slot,
            value.Reason,
            value.UpdatedTime)));
    });

app.MapGet(
    "/api/v1/ingestion/capacity",
    async (
        IIngestionOperationsQuery query,
        MvpConfiguration configuration,
        CancellationToken cancellationToken) =>
    {
        var report = await query.GetCapacityReportAsync(cancellationToken);
        return Results.Ok(new IngestionCapacityResponse(
            report.GeneratedAt,
            report.TotalBytes,
            configuration.Storage.CapacityReviewMinimumDays,
            configuration.Storage.PartitionAheadMonths,
            configuration.Storage.RebuildableHotRetentionDays,
            configuration.Storage.OperationalRetentionDays,
            report.EventsLast24Hours,
            report.RawBytesLast24Hours,
            report.SwapsLast24Hours,
            report.MarketSnapshotsLast24Hours,
            report.OldestRawEventTime,
            report.NewestRawEventTime,
            report.Tables.Select(value => new IngestionCapacityTableResponse(
                    value.TableName,
                    value.EstimatedRows,
                    value.DataBytes,
                    value.IndexBytes,
                    value.TotalBytes,
                    value.IsPartitioned))
                .ToArray()));
    });

app.MapPost(
    "/api/v1/intelligence/evaluate",
    (
        IntelligenceEvaluationRequest request,
        IntelligenceEvaluationService service) =>
    {
        if (request.InputAsOfTime < request.DiscoveredAt)
        {
            return Results.BadRequest(new
            {
                error = "inputAsOfTime cannot be earlier than discoveredAt."
            });
        }

        SellQuoteEvidence? sellQuote = null;
        if (request.SellQuote is not null)
        {
            if (!Enum.TryParse<SellQuoteStatus>(
                    request.SellQuote.Status,
                    ignoreCase: true,
                    out var status))
            {
                return Results.BadRequest(new
                {
                    error = $"Unknown sell quote status '{request.SellQuote.Status}'."
                });
            }

            sellQuote = new SellQuoteEvidence(
                status,
                request.SellQuote.InputBaseAmount,
                request.SellQuote.OutputQuoteAmount,
                request.SellQuote.PriceImpactBasisPoints,
                request.SellQuote.AsOfTime,
                request.SellQuote.AdapterVersion,
                request.SellQuote.FailureReason);
        }

        var result = service.Evaluate(new IntelligenceEvaluationInput(
            request.TokenName,
            request.Symbol,
            request.DiscoveredAt,
            request.InputAsOfTime,
            request.HasUsableLiquidity,
            new RiskEvidenceSnapshot(
                request.InputAsOfTime,
                request.MarketAsOfTime,
                request.QuoteReserveRaw,
                request.EntryPriceImpactBasisPoints,
                request.LiquidityDropBasisPoints,
                request.MintAuthorityEnabled,
                request.FreezeAuthorityEnabled,
                request.AdapterAuthorityRisk,
                request.CreatorHoldingBasisPoints,
                request.Top10HoldingBasisPoints,
                request.PoolVersionSupported,
                request.IsFinalized,
                request.IsReconciled,
                sellQuote)));
        return Results.Ok(ToIntelligenceResponse(result));
    });

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

app.Run();

static RadarCandidateResponse ToResponse(RadarCandidateReadModel value) => new(
    value.TokenAddress,
    value.Name,
    value.Symbol,
    value.Status.ToString(),
    value.DiscoveredAt,
    value.UpdatedAt,
    value.Reason,
    value.PoolCount,
    value.QuoteTokenAddress,
    value.LatestFeaturesJson,
    value.LatestTheme is null
        ? null
        : new ThemeMatchResponse(
            value.LatestTheme.Matched,
            value.LatestTheme.Blocked,
            value.LatestTheme.ConfigurationValid,
            value.LatestTheme.ThemeScore,
            value.LatestTheme.MatchedThemes,
            value.LatestTheme.MatchReasons,
            value.LatestTheme.InputAsOfTime,
            value.LatestTheme.ConfigurationVersion),
    value.LatestRisk is null
        ? null
        : new RiskAssessmentResponse(
            value.LatestRisk.OverallScore,
            value.LatestRisk.RiskLevel.ToString(),
            value.LatestRisk.HardReject,
            value.LatestRisk.RuleResults.Select(rule => new RiskRuleResponse(
                    rule.RuleId,
                    rule.Outcome.ToString(),
                    rule.HardReject,
                    rule.RiskScore,
                    rule.Reason))
                .ToArray(),
            value.LatestRisk.Reasons,
            value.LatestRisk.InputAsOfTime,
            value.LatestRisk.RiskModelVersion));

static IntelligenceEvaluationResponse ToIntelligenceResponse(
    IntelligenceEvaluationResult value) => new(
    new ThemeMatchResponse(
        value.Theme.Matched,
        value.Theme.Blocked,
        value.Theme.ConfigurationValid,
        value.Theme.ThemeScore,
        value.Theme.MatchedThemes,
        value.Theme.MatchReasons,
        value.Theme.InputAsOfTime,
        value.Theme.ConfigurationVersion),
    new RiskAssessmentResponse(
        value.Risk.OverallScore,
        value.Risk.RiskLevel.ToString(),
        value.Risk.HardReject,
        value.Risk.RuleResults.Select(rule => new RiskRuleResponse(
                rule.RuleId,
                rule.Outcome.ToString(),
                rule.HardReject,
                rule.RiskScore,
                rule.Reason))
            .ToArray(),
        value.Risk.Reasons,
        value.Risk.InputAsOfTime,
        value.Risk.RiskModelVersion),
    new CandidateEligibilityResponse(
        value.Candidate.Status.ToString(),
        value.Candidate.Reasons,
        value.Candidate.EvaluatedAt));

public partial class Program;
