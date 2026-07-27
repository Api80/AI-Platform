using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class CryptoIntelligenceDbContextFactory
    : IDesignTimeDbContextFactory<CryptoIntelligenceDbContext>
{
    public CryptoIntelligenceDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CRYPTO_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=crypto_intelligence;Username=postgres";
        var options = new DbContextOptionsBuilder<CryptoIntelligenceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new CryptoIntelligenceDbContext(options);
    }
}
