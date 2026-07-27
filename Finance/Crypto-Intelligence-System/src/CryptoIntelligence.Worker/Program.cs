using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Application.Radar;
using CryptoIntelligence.Infrastructure.Persistence;
using CryptoIntelligence.Infrastructure.Solana;
using CryptoIntelligence.Infrastructure.Solana.Raydium;
using CryptoIntelligence.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ");

var mvpConfiguration = builder.Configuration
    .GetSection(MvpConfiguration.SectionName)
    .Get<MvpConfiguration>();
MvpConfigurationValidator.ThrowIfInvalid(mvpConfiguration);
var snapshot = ConfigurationSnapshotFactory.Create(mvpConfiguration!, DateTimeOffset.UtcNow);
builder.Services.AddSingleton(mvpConfiguration!);
builder.Services.AddSingleton(snapshot);
builder.Services.AddSingleton<IntelligenceEvaluationService>();
builder.Services.AddSingleton<ISolanaTransactionAdapter>(
    RaydiumTransactionAdapter.CreatePinned(
        mvpConfiguration!.Source.AdapterVersion));
builder.Services.AddSingleton<IRaydiumSellQuoteEvidenceSource>(
    new RaydiumCpmmSellQuoteEvidenceSource(
        mvpConfiguration.Source.AdapterVersion,
        mvpConfiguration.Risk.HardReject.MaximumMarketDataAgeSeconds));

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("CRYPTO_DB_CONNECTION")
    ?? throw new InvalidOperationException(
        "PostgreSQL connection must be supplied through ConnectionStrings__Postgres " +
        "or CRYPTO_DB_CONNECTION.");
builder.Services.AddCryptoIntelligencePersistence(connectionString);
builder.Services.AddScoped<IRawEventHandler, SolanaAdapterRawEventHandler>();
builder.Services.AddScoped<IProjectionEventHandler, RadarProjectionHandler>();
builder.Services.AddScoped<DurableRawEventDispatcher>();
builder.Services.AddScoped<RiskEvidenceCollector>();
builder.Services.AddScoped<IntelligenceAssessmentService>();
builder.Services.AddScoped(provider => new SolanaBackfillReconciliationService(
    provider.GetRequiredService<ISolanaBackfillSource>(),
    provider.GetRequiredService<ISolanaTransactionSource>(),
    provider.GetRequiredService<IRawEventStore>(),
    provider.GetRequiredService<IIngestionReconciliationStore>(),
    mvpConfiguration.Source.RpcSourceName,
    mvpConfiguration.Source.BackfillMaximumSlotsPerCycle,
    mvpConfiguration.Source.BackfillMaximumSignaturesPerCycle));
ConfigureSolanaSources(builder.Services, mvpConfiguration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

static void ConfigureSolanaSources(
    IServiceCollection services,
    MvpConfiguration configuration)
{
    var webSocketUrl = Environment.GetEnvironmentVariable("SOLANA_RPC_WS_URL");
    var primaryHttpUrl = Environment.GetEnvironmentVariable("SOLANA_RPC_HTTP_URL");
    if (string.IsNullOrWhiteSpace(webSocketUrl) ||
        string.IsNullOrWhiteSpace(primaryHttpUrl))
    {
        return;
    }

    var webSocketEndpoint = RequireEndpoint(webSocketUrl, "SOLANA_RPC_WS_URL", "ws", "wss");
    var primaryEndpoint = RequireEndpoint(
        primaryHttpUrl,
        "SOLANA_RPC_HTTP_URL",
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps);
    services.AddSingleton<IDiscoveryConnectionObserver, LoggingDiscoveryConnectionObserver>();
    services.AddSingleton<ISolanaDiscoverySource>(provider =>
        new SolanaWebSocketDiscoverySource(
            webSocketEndpoint,
            configuration.Source.ProgramIds,
            configuration.Source.DiscoveryCommitment,
            provider.GetRequiredService<IDiscoveryConnectionObserver>()));
    services.AddSingleton<ISolanaTransactionSource>(_ =>
    {
        var sources = new List<ISolanaTransactionSource>
        {
            CreateRpcSource(primaryEndpoint, configuration.Source.RpcSourceName)
        };
        var fallbackUrl = Environment.GetEnvironmentVariable(
            "SOLANA_RPC_FALLBACK_HTTP_URL");
        if (!string.IsNullOrWhiteSpace(fallbackUrl))
        {
            sources.Add(CreateRpcSource(
                RequireEndpoint(
                    fallbackUrl,
                    "SOLANA_RPC_FALLBACK_HTTP_URL",
                    Uri.UriSchemeHttp,
                    Uri.UriSchemeHttps),
                configuration.Source.FallbackRpcSourceName ?? "fallback"));
        }

        return new FallbackSolanaTransactionSource(sources);
    });
    services.AddSingleton<ISolanaBackfillSource>(_ =>
    {
        var sources = new List<ISolanaBackfillSource>
        {
            CreateBackfillSource(primaryEndpoint, configuration.Source.RpcSourceName)
        };
        var fallbackUrl = Environment.GetEnvironmentVariable(
            "SOLANA_RPC_FALLBACK_HTTP_URL");
        if (!string.IsNullOrWhiteSpace(fallbackUrl))
        {
            sources.Add(CreateBackfillSource(
                RequireEndpoint(
                    fallbackUrl,
                    "SOLANA_RPC_FALLBACK_HTTP_URL",
                    Uri.UriSchemeHttp,
                    Uri.UriSchemeHttps),
                configuration.Source.FallbackRpcSourceName ?? "fallback"));
        }

        return new FallbackSolanaBackfillSource(sources);
    });
    services.AddSingleton<ISolanaTokenRiskEvidenceSource>(_ =>
    {
        var sources = new List<ISolanaTokenRiskEvidenceSource>
        {
            CreateTokenEvidenceSource(
                primaryEndpoint,
                configuration.Source.RpcSourceName)
        };
        var fallbackUrl = Environment.GetEnvironmentVariable(
            "SOLANA_RPC_FALLBACK_HTTP_URL");
        if (!string.IsNullOrWhiteSpace(fallbackUrl))
        {
            sources.Add(CreateTokenEvidenceSource(
                RequireEndpoint(
                    fallbackUrl,
                    "SOLANA_RPC_FALLBACK_HTTP_URL",
                    Uri.UriSchemeHttp,
                    Uri.UriSchemeHttps),
                configuration.Source.FallbackRpcSourceName ?? "fallback"));
        }

        return new FallbackSolanaTokenRiskEvidenceSource(sources);
    });
    services.AddScoped<IRawEventHandler, SolanaDiscoveryRawEventHandler>();
    services.AddScoped<IProjectionEventHandler, AutomatedRiskAssessmentHandler>();
}

static SolanaRpcTransactionSource CreateRpcSource(Uri endpoint, string sourceName) =>
    new(
        new HttpClient
        {
            BaseAddress = endpoint,
            Timeout = TimeSpan.FromSeconds(20)
        },
        sourceName);

static SolanaRpcBackfillSource CreateBackfillSource(
    Uri endpoint,
    string sourceName) =>
    new(
        new HttpClient
        {
            BaseAddress = endpoint,
            Timeout = TimeSpan.FromSeconds(30)
        },
        sourceName);

static SolanaTokenRiskEvidenceSource CreateTokenEvidenceSource(
    Uri endpoint,
    string sourceName) =>
    new(
        new HttpClient
        {
            BaseAddress = endpoint,
            Timeout = TimeSpan.FromSeconds(30)
        },
        sourceName);

static Uri RequireEndpoint(
    string value,
    string variableName,
    params string[] allowedSchemes)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
        !allowedSchemes.Contains(endpoint.Scheme, StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"{variableName} must be an absolute {string.Join('/', allowedSchemes)} URL.");
    }

    return endpoint;
}
