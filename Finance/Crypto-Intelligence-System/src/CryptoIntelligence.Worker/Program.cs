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
    var endpoints = SolanaRuntimeEndpoints.Create(
        configuration,
        Environment.GetEnvironmentVariable("SOLANA_RPC_WS_URL"),
        Environment.GetEnvironmentVariable("SOLANA_RPC_HTTP_URL"),
        Environment.GetEnvironmentVariable("SOLANA_RPC_FALLBACK_HTTP_URL"));
    if (endpoints is null)
    {
        return;
    }

    services.AddSingleton<IDiscoveryConnectionObserver, LoggingDiscoveryConnectionObserver>();
    services.AddSingleton<ISolanaDiscoverySource>(provider =>
        new SolanaWebSocketDiscoverySource(
            endpoints.WebSocket,
            configuration.Source.ProgramIds,
            configuration.Source.DiscoveryCommitment,
            provider.GetRequiredService<IDiscoveryConnectionObserver>()));
    services.AddSingleton<ISolanaTransactionSource>(_ =>
    {
        var sources = new List<ISolanaTransactionSource>
        {
            CreateRpcSource(endpoints.PrimaryHttp, configuration.Source.RpcSourceName)
        };
        if (endpoints.FallbackHttp is not null)
        {
            sources.Add(CreateRpcSource(
                endpoints.FallbackHttp,
                configuration.Source.FallbackRpcSourceName ?? "fallback"));
        }

        return new FallbackSolanaTransactionSource(sources);
    });
    services.AddSingleton<ISolanaBackfillSource>(_ =>
    {
        var sources = new List<ISolanaBackfillSource>
        {
            CreateBackfillSource(endpoints.PrimaryHttp, configuration.Source.RpcSourceName)
        };
        if (endpoints.FallbackHttp is not null)
        {
            sources.Add(CreateBackfillSource(
                endpoints.FallbackHttp,
                configuration.Source.FallbackRpcSourceName ?? "fallback"));
        }

        return new FallbackSolanaBackfillSource(sources);
    });
    services.AddSingleton<ISolanaTokenRiskEvidenceSource>(_ =>
    {
        var sources = new List<ISolanaTokenRiskEvidenceSource>
        {
            CreateTokenEvidenceSource(
                endpoints.PrimaryHttp,
                configuration.Source.RpcSourceName)
        };
        if (endpoints.FallbackHttp is not null)
        {
            sources.Add(CreateTokenEvidenceSource(
                endpoints.FallbackHttp,
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
