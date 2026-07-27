using CryptoIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CryptoIntelligence.Infrastructure.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Model_contains_versioned_configuration_snapshot()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(
            "CryptoIntelligence.Infrastructure.Persistence.Entities.ConfigurationSnapshotEntity");

        Assert.NotNull(entity);
        Assert.Equal("configuration_snapshots", entity.GetTableName());
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Single().Name == "ConfigurationHash");
    }

    [Fact]
    public void Initial_migration_generates_idempotent_postgresql_script()
    {
        using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            fromMigration: null,
            toMigration: null,
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("configuration_snapshots", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE", script, StringComparison.Ordinal);
    }

    private static CryptoIntelligenceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CryptoIntelligenceDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=crypto_intelligence_tests;" +
                "Username=postgres")
            .Options;
        return new CryptoIntelligenceDbContext(options);
    }
}
