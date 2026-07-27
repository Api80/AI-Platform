using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CryptoIntelligence.Application.Ingestion;

namespace CryptoIntelligence.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddCryptoIntelligencePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<CryptoIntelligenceDbContext>(
            options => options.UseNpgsql(connectionString));
        services.AddScoped<IRawEventStore, PostgresRawEventStore>();
        services.AddScoped<INormalizedEventStore, PostgresNormalizedEventStore>();
        return services;
    }
}
