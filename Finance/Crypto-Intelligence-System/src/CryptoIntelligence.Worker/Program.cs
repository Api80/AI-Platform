using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Infrastructure.Persistence;
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

var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("CRYPTO_DB_CONNECTION")
    ?? throw new InvalidOperationException(
        "PostgreSQL connection must be supplied through ConnectionStrings__Postgres " +
        "or CRYPTO_DB_CONNECTION.");
builder.Services.AddCryptoIntelligencePersistence(connectionString);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
