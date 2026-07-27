using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Application.Intelligence;
using CryptoIntelligence.Application.Radar;

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
        services.AddScoped<
            IIngestionReconciliationStore,
            PostgresIngestionReconciliationStore>();
        services.AddScoped<
            IIngestionOperationsQuery,
            PostgresIngestionOperationsQuery>();
        services.AddScoped<INormalizedEventStore, PostgresNormalizedEventStore>();
        services.AddScoped<IRadarProjectionStore, PostgresRadarProjectionStore>();
        services.AddScoped<IRadarQueryService, PostgresRadarQueryService>();
        services.AddScoped<
            IIntelligenceAssessmentStore,
            PostgresIntelligenceAssessmentStore>();
        return services;
    }
}
