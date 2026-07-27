using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class CryptoIntelligenceDbContext(
    DbContextOptions<CryptoIntelligenceDbContext> options)
    : DbContext(options)
{
    public DbSet<ConfigurationSnapshotEntity> ConfigurationSnapshots =>
        Set<ConfigurationSnapshotEntity>();

    public DbSet<RawBlockchainEventEntity> RawBlockchainEvents =>
        Set<RawBlockchainEventEntity>();

    public DbSet<IngestionCheckpointEntity> IngestionCheckpoints =>
        Set<IngestionCheckpointEntity>();

    public DbSet<IngestionSlotStateEntity> IngestionSlotStates =>
        Set<IngestionSlotStateEntity>();

    public DbSet<NormalizedDomainEventEntity> NormalizedDomainEvents =>
        Set<NormalizedDomainEventEntity>();

    public DbSet<TokenEntity> Tokens => Set<TokenEntity>();
    public DbSet<LiquidityPoolEntity> LiquidityPools => Set<LiquidityPoolEntity>();
    public DbSet<WalletEntity> Wallets => Set<WalletEntity>();
    public DbSet<SwapEventEntity> SwapEvents => Set<SwapEventEntity>();
    public DbSet<LiquidityEventEntity> LiquidityEvents => Set<LiquidityEventEntity>();
    public DbSet<MarketSnapshotEntity> MarketSnapshots => Set<MarketSnapshotEntity>();
    public DbSet<TokenCandidateEntity> TokenCandidates => Set<TokenCandidateEntity>();
    public DbSet<FeatureSnapshotEntity> FeatureSnapshots => Set<FeatureSnapshotEntity>();
    public DbSet<ThemeMatchEntity> ThemeMatches => Set<ThemeMatchEntity>();
    public DbSet<RiskAssessmentEntity> RiskAssessments =>
        Set<RiskAssessmentEntity>();
    public DbSet<AutomatedAssessmentAttemptEntity> AutomatedAssessmentAttempts =>
        Set<AutomatedAssessmentAttemptEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var configuration = modelBuilder.Entity<ConfigurationSnapshotEntity>();
        configuration.ToTable("configuration_snapshots");
        configuration.HasKey(entity => entity.Id);
        configuration.Property(entity => entity.Id).HasColumnName("id");
        configuration
            .Property(entity => entity.ConfigurationVersion)
            .HasColumnName("configuration_version")
            .HasMaxLength(100)
            .IsRequired();
        configuration
            .Property(entity => entity.ConfigurationHash)
            .HasColumnName("configuration_hash")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        configuration
            .Property(entity => entity.CanonicalJson)
            .HasColumnName("canonical_json")
            .HasColumnType("jsonb")
            .IsRequired();
        configuration
            .Property(entity => entity.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        configuration
            .HasIndex(entity => entity.ConfigurationHash)
            .IsUnique()
            .HasDatabaseName("ux_configuration_snapshots_hash");
        configuration
            .HasIndex(entity => new { entity.ConfigurationVersion, entity.CreatedAtUtc })
            .HasDatabaseName("ix_configuration_snapshots_version_created");

        modelBuilder.ConfigureIngestion();
        modelBuilder.ConfigureRadar();
        modelBuilder.ConfigureIntelligence();
        modelBuilder.ApplySnakeCaseColumns();
    }
}
