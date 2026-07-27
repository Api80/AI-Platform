using System.Text;
using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

internal static class RadarModelConfiguration
{
    public static void ConfigureRadar(this ModelBuilder modelBuilder)
    {
        ConfigureTokens(modelBuilder);
        ConfigurePools(modelBuilder);
        ConfigureWallets(modelBuilder);
        ConfigureSwaps(modelBuilder);
        ConfigureLiquidityEvents(modelBuilder);
        ConfigureMarketSnapshots(modelBuilder);
        ConfigureCandidates(modelBuilder);
        ConfigureFeatures(modelBuilder);
    }

    private static void ConfigureTokens(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TokenEntity>();
        entity.ToTable("tokens");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Chain).HasMaxLength(32);
        entity.Property(value => value.Network).HasMaxLength(64);
        entity.Property(value => value.MintAddress).HasMaxLength(64);
        entity.Property(value => value.Name).HasMaxLength(200);
        entity.Property(value => value.Symbol).HasMaxLength(50);
        entity.Property(value => value.LifecycleStatus).HasConversion<string>().HasMaxLength(32);
        Timestamps(
            entity.Property(value => value.CreatedTime),
            entity.Property(value => value.FirstObservedTime),
            entity.Property(value => value.UpdatedTime));
        entity.HasIndex(value => new { value.Chain, value.Network, value.MintAddress })
            .IsUnique()
            .HasDatabaseName("ux_tokens_chain_network_mint");
    }

    private static void ConfigurePools(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LiquidityPoolEntity>();
        entity.ToTable("liquidity_pools");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Chain).HasMaxLength(32);
        entity.Property(value => value.Network).HasMaxLength(64);
        entity.Property(value => value.PoolAddress).HasMaxLength(64);
        entity.Property(value => value.Dex).HasMaxLength(50);
        entity.Property(value => value.ProgramId).HasMaxLength(64);
        entity.Property(value => value.BaseReserve).HasPrecision(38, 0);
        entity.Property(value => value.QuoteReserve).HasPrecision(38, 0);
        entity.Property(value => value.LifecycleStatus).HasConversion<string>().HasMaxLength(32);
        Timestamps(
            entity.Property(value => value.CreatedTime),
            entity.Property(value => value.FirstObservedTime),
            entity.Property(value => value.UpdatedTime));
        entity.HasIndex(value => new { value.Chain, value.Network, value.PoolAddress })
            .IsUnique()
            .HasDatabaseName("ux_pools_chain_network_address");
        entity.HasOne<TokenEntity>().WithMany().HasForeignKey(value => value.BaseTokenId);
        entity.HasOne<TokenEntity>().WithMany().HasForeignKey(value => value.QuoteTokenId);
    }

    private static void ConfigureWallets(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WalletEntity>();
        entity.ToTable("wallets");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Chain).HasMaxLength(32);
        entity.Property(value => value.Network).HasMaxLength(64);
        entity.Property(value => value.Address).HasMaxLength(64);
        Timestamps(
            entity.Property(value => value.FirstSeenTime),
            entity.Property(value => value.LastSeenTime),
            entity.Property(value => value.CreatedTime),
            entity.Property(value => value.UpdatedTime));
        entity.HasIndex(value => new { value.Chain, value.Network, value.Address })
            .IsUnique()
            .HasDatabaseName("ux_wallets_chain_network_address");
    }

    private static void ConfigureSwaps(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SwapEventEntity>();
        entity.ToTable("swap_events");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Side).HasConversion<string>().HasMaxLength(16);
        entity.Property(value => value.BaseAmount).HasPrecision(38, 0);
        entity.Property(value => value.QuoteAmount).HasPrecision(38, 0);
        entity.Property(value => value.PriceInQuote).HasPrecision(38, 18);
        Timestamps(
            entity.Property(value => value.EventTime),
            entity.Property(value => value.ObservedTime));
        entity.HasIndex(value => new { value.RawEventId, value.PoolId, value.SwapIndex })
            .IsUnique()
            .HasDatabaseName("ux_swaps_raw_pool_index");
        entity.HasIndex(value => new { value.PoolId, value.EventTime })
            .HasDatabaseName("ix_swaps_pool_time");
        entity.HasOne<RawBlockchainEventEntity>().WithMany().HasForeignKey(value => value.RawEventId);
        entity.HasOne<LiquidityPoolEntity>().WithMany().HasForeignKey(value => value.PoolId);
        entity.HasOne<WalletEntity>().WithMany().HasForeignKey(value => value.TraderWalletId);
        entity.HasOne<TokenEntity>().WithMany().HasForeignKey(value => value.BaseTokenId);
        entity.HasOne<TokenEntity>().WithMany().HasForeignKey(value => value.QuoteTokenId);
    }

    private static void ConfigureLiquidityEvents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<LiquidityEventEntity>();
        entity.ToTable("liquidity_events");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.ChangeType).HasMaxLength(32);
        entity.Property(value => value.BaseAmount).HasPrecision(38, 0);
        entity.Property(value => value.QuoteAmount).HasPrecision(38, 0);
        entity.Property(value => value.BaseReserveAfter).HasPrecision(38, 0);
        entity.Property(value => value.QuoteReserveAfter).HasPrecision(38, 0);
        Timestamps(entity.Property(value => value.EventTime));
        entity.HasIndex(value => new
        {
            value.RawEventId,
            value.PoolId,
            value.LiquidityIndex
        })
            .IsUnique()
            .HasDatabaseName("ux_liquidity_events_raw_pool_index");
        entity.HasOne<RawBlockchainEventEntity>().WithMany().HasForeignKey(value => value.RawEventId);
        entity.HasOne<LiquidityPoolEntity>().WithMany().HasForeignKey(value => value.PoolId);
    }

    private static void ConfigureMarketSnapshots(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MarketSnapshotEntity>();
        entity.ToTable("market_snapshots");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.PriceInQuote).HasPrecision(38, 18);
        entity.Property(value => value.BaseVolume).HasPrecision(38, 0);
        entity.Property(value => value.QuoteVolume).HasPrecision(38, 0);
        entity.Property(value => value.BaseReserve).HasPrecision(38, 0);
        entity.Property(value => value.QuoteReserve).HasPrecision(38, 0);
        entity.Property(value => value.LiquidityInQuote).HasPrecision(38, 0);
        entity.Property(value => value.TraderAddress).HasMaxLength(64);
        Timestamps(
            entity.Property(value => value.AsOfTime),
            entity.Property(value => value.CreatedTime));
        entity.HasIndex(value => new { value.PoolId, value.AsOfSlot, value.EventIndex })
            .IsUnique()
            .HasDatabaseName("ux_market_snapshots_pool_slot_index");
        entity.HasIndex(value => new { value.PoolId, value.AsOfTime })
            .HasDatabaseName("ix_market_snapshots_pool_time");
    }

    private static void ConfigureCandidates(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TokenCandidateEntity>();
        entity.ToTable("token_candidates");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Status).HasConversion<string>().HasMaxLength(32);
        entity.Property(value => value.Reason).HasMaxLength(1_000);
        Timestamps(
            entity.Property(value => value.DiscoveredAt),
            entity.Property(value => value.UpdatedAt));
        entity.HasIndex(value => value.TokenId)
            .IsUnique()
            .HasDatabaseName("ux_token_candidates_token");
        entity.HasIndex(value => new { value.Status, value.UpdatedAt })
            .HasDatabaseName("ix_token_candidates_status_updated");
        entity.HasOne<TokenEntity>().WithMany().HasForeignKey(value => value.TokenId);
    }

    private static void ConfigureFeatures(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FeatureSnapshotEntity>();
        entity.ToTable("feature_snapshots");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.EntityType).HasMaxLength(50);
        entity.Property(value => value.EntityNaturalKey).HasMaxLength(128);
        entity.Property(value => value.FeatureSetVersion).HasMaxLength(100);
        entity.Property(value => value.Values).HasColumnType("jsonb");
        Timestamps(
            entity.Property(value => value.AsOfTime),
            entity.Property(value => value.ComputedTime));
        entity.HasIndex(value => new
        {
            value.EntityType,
            value.EntityNaturalKey,
            value.FeatureSetVersion,
            value.AsOfSlot
        })
            .IsUnique()
            .HasDatabaseName("ux_feature_snapshots_entity_version_slot");
    }

    private static void Timestamps(
        params Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTimeOffset>[] properties)
    {
        foreach (var property in properties)
        {
            property.HasColumnType("timestamp with time zone");
        }
    }

    public static void ApplySnakeCaseColumns(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
