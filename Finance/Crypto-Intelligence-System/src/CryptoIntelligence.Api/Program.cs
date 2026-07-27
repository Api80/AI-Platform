using CryptoIntelligence.Api;
using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Contracts;
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
        "M2 New Token Radar",
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
    value.LatestFeaturesJson);

public partial class Program;
