using CryptoIntelligence.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

internal static class IntelligenceModelConfiguration
{
    public static void ConfigureIntelligence(this ModelBuilder modelBuilder)
    {
        ConfigureThemeMatches(modelBuilder);
        ConfigureRiskAssessments(modelBuilder);
        ConfigureCandidateAssessmentLinks(modelBuilder);
    }

    private static void ConfigureThemeMatches(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ThemeMatchEntity>();
        entity.ToTable("theme_matches");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.ConfigurationVersion)
            .HasMaxLength(100);
        entity.Property(value => value.MatchedThemes)
            .HasColumnType("jsonb");
        entity.Property(value => value.MatchReasons)
            .HasColumnType("jsonb");
        entity.Property(value => value.InputAsOfTime)
            .HasColumnType("timestamp with time zone");
        entity.Property(value => value.CreatedTime)
            .HasColumnType("timestamp with time zone");
        entity.HasIndex(value => new
        {
            value.TokenId,
            value.ConfigurationVersion,
            value.InputAsOfTime
        })
            .IsUnique()
            .HasDatabaseName("ux_theme_matches_token_version_time");
        entity.HasIndex(value => new { value.TokenId, value.InputAsOfTime })
            .HasDatabaseName("ix_theme_matches_token_time");
        entity.HasOne<TokenEntity>()
            .WithMany()
            .HasForeignKey(value => value.TokenId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureRiskAssessments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RiskAssessmentEntity>();
        entity.ToTable("risk_assessments");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.RiskLevel)
            .HasConversion<string>()
            .HasMaxLength(32);
        entity.Property(value => value.RuleResults)
            .HasColumnType("jsonb");
        entity.Property(value => value.Reasons)
            .HasColumnType("jsonb");
        entity.Property(value => value.Evidence)
            .HasColumnType("jsonb");
        entity.Property(value => value.RiskModelVersion)
            .HasMaxLength(100);
        entity.Property(value => value.InputAsOfTime)
            .HasColumnType("timestamp with time zone");
        entity.Property(value => value.CreatedTime)
            .HasColumnType("timestamp with time zone");
        entity.HasIndex(value => new
        {
            value.TokenId,
            value.RiskModelVersion,
            value.InputAsOfTime
        })
            .IsUnique()
            .HasDatabaseName("ux_risk_assessments_token_version_time");
        entity.HasIndex(value => new { value.TokenId, value.InputAsOfTime })
            .HasDatabaseName("ix_risk_assessments_token_time");
        entity.HasOne<TokenEntity>()
            .WithMany()
            .HasForeignKey(value => value.TokenId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<FeatureSnapshotEntity>()
            .WithMany()
            .HasForeignKey(value => value.FeatureSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCandidateAssessmentLinks(
        ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TokenCandidateEntity>();
        entity.Property(value => value.LatestEvaluationAsOfTime)
            .HasColumnType("timestamp with time zone");
        entity.HasOne<ThemeMatchEntity>()
            .WithMany()
            .HasForeignKey(value => value.LatestThemeMatchId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<RiskAssessmentEntity>()
            .WithMany()
            .HasForeignKey(value => value.LatestRiskAssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
