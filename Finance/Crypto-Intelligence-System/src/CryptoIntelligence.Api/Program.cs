using CryptoIntelligence.Api;
using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Contracts;
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
        "M1 Foundation",
        snapshot.ConfigurationVersion,
        snapshot.ConfigurationHash,
        DateTimeOffset.UtcNow));

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

public partial class Program;
