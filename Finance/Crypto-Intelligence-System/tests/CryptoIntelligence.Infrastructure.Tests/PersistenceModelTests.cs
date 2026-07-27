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

    [Fact]
    public void Model_contains_reliable_ingestion_tables_and_unique_event_identity()
    {
        using var context = CreateContext();
        var rawEvent = context.Model.FindEntityType(
            "CryptoIntelligence.Infrastructure.Persistence.Entities.RawBlockchainEventEntity");
        var checkpoint = context.Model.FindEntityType(
            "CryptoIntelligence.Infrastructure.Persistence.Entities.IngestionCheckpointEntity");
        var slotState = context.Model.FindEntityType(
            "CryptoIntelligence.Infrastructure.Persistence.Entities.IngestionSlotStateEntity");
        var normalizedEvent = context.Model.FindEntityType(
            "CryptoIntelligence.Infrastructure.Persistence.Entities.NormalizedDomainEventEntity");

        Assert.Equal("raw_blockchain_events", rawEvent?.GetTableName());
        Assert.Equal("ingestion_checkpoints", checkpoint?.GetTableName());
        Assert.Equal("ingestion_slot_states", slotState?.GetTableName());
        Assert.Equal("normalized_domain_events", normalizedEvent?.GetTableName());
        Assert.Contains(
            rawEvent!.GetIndexes(),
            index => index.IsUnique &&
                     index.Properties.Single().Name == "EventId");
    }

    [Fact]
    public void Latest_migration_script_contains_ingestion_schema()
    {
        using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();

        var script = migrator.GenerateScript(
            fromMigration: null,
            toMigration: null,
            MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("raw_blockchain_events", script, StringComparison.Ordinal);
        Assert.Contains("ingestion_checkpoints", script, StringComparison.Ordinal);
        Assert.Contains("ingestion_slot_states", script, StringComparison.Ordinal);
        Assert.Contains("ux_raw_events_event_id", script, StringComparison.Ordinal);
        Assert.Contains("normalized_domain_events", script, StringComparison.Ordinal);
        Assert.Contains(
            "ux_normalized_events_parser_identity",
            script,
            StringComparison.Ordinal);
        Assert.Contains("tokens", script, StringComparison.Ordinal);
        Assert.Contains("liquidity_pools", script, StringComparison.Ordinal);
        Assert.Contains("swap_events", script, StringComparison.Ordinal);
        Assert.Contains("liquidity_events", script, StringComparison.Ordinal);
        Assert.Contains("market_snapshots", script, StringComparison.Ordinal);
        Assert.Contains("token_candidates", script, StringComparison.Ordinal);
        Assert.Contains("feature_snapshots", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Model_contains_radar_projection_tables()
    {
        using var context = CreateContext();
        string[] tables =
        [
            "tokens",
            "liquidity_pools",
            "wallets",
            "swap_events",
            "liquidity_events",
            "market_snapshots",
            "token_candidates",
            "feature_snapshots"
        ];

        var actual = context.Model.GetEntityTypes()
            .Select(value => value.GetTableName())
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(tables, table => Assert.Contains(table, actual));
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
