using CryptoIntelligence.Application.Configuration;

namespace CryptoIntelligence.Application.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void Valid_development_configuration_creates_stable_snapshot()
    {
        var configuration = ValidConfiguration();
        var time = new DateTimeOffset(2026, 7, 28, 0, 0, 0, TimeSpan.Zero);

        var first = ConfigurationSnapshotFactory.Create(configuration, time);
        var second = ConfigurationSnapshotFactory.Create(configuration, time);

        Assert.Equal(first.ConfigurationHash, second.ConfigurationHash);
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(64, first.ConfigurationHash.Length);
    }

    [Fact]
    public void Missing_adapter_is_rejected()
    {
        var configuration = ValidConfiguration().WithSource(new SourceConfiguration
        {
            PoolAdapter = "RaydiumCpmmV1",
            AdapterVersion = "adapter-v1",
            LaunchLabParserVersion = "launch-v1",
            CpmmParserVersion = "cpmm-v1",
            ProgramIds = ["LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj"],
            FixtureCoverageStartSlot = 339103624,
            RpcSourceName = "development"
        });

        var errors = MvpConfigurationValidator.Validate(configuration);

        Assert.Contains(errors, error => error.Path == "source.launchAdapter");
    }

    [Fact]
    public void Formal_run_requires_start_slot_fallback_and_reconciliation()
    {
        var original = ValidConfiguration();
        var configuration = new MvpConfiguration
        {
            ConfigurationVersion = original.ConfigurationVersion,
            FormalRun = true,
            Source = new SourceConfiguration
            {
                LaunchAdapter = original.Source.LaunchAdapter,
                PoolAdapter = original.Source.PoolAdapter,
                AdapterVersion = original.Source.AdapterVersion,
                LaunchLabParserVersion = original.Source.LaunchLabParserVersion,
                CpmmParserVersion = original.Source.CpmmParserVersion,
                ProgramIds = original.Source.ProgramIds,
                FixtureCoverageStartSlot = original.Source.FixtureCoverageStartSlot,
                RpcSourceName = original.Source.RpcSourceName,
                RequireReconciledData = false
            },
            Radar = original.Radar,
            Storage = original.Storage,
            Theme = original.Theme,
            Risk = original.Risk
        };

        var errors = MvpConfigurationValidator.Validate(configuration);

        Assert.Contains(errors, error => error.Path == "source.historicalRunStartSlot");
        Assert.Contains(errors, error => error.Path == "source.fallbackRpcSourceName");
        Assert.Contains(errors, error => error.Path == "source.requireReconciledData");
    }

    [Fact]
    public void Formal_run_requires_distinct_source_names()
    {
        var original = ValidConfiguration();
        var configuration = new MvpConfiguration
        {
            ConfigurationVersion = original.ConfigurationVersion,
            FormalRun = true,
            Source = new SourceConfiguration
            {
                LaunchAdapter = original.Source.LaunchAdapter,
                PoolAdapter = original.Source.PoolAdapter,
                AdapterVersion = original.Source.AdapterVersion,
                LaunchLabParserVersion = original.Source.LaunchLabParserVersion,
                CpmmParserVersion = original.Source.CpmmParserVersion,
                ProgramIds = original.Source.ProgramIds,
                FixtureCoverageStartSlot =
                    original.Source.FixtureCoverageStartSlot,
                HistoricalRunStartSlot = 1,
                RpcSourceName = "same",
                FallbackRpcSourceName = "same",
                RequireReconciledData = true
            },
            Radar = original.Radar,
            Storage = original.Storage,
            Theme = original.Theme,
            Risk = original.Risk
        };

        var errors = MvpConfigurationValidator.Validate(configuration);

        Assert.Contains(
            errors,
            error => error.Path == "source.fallbackRpcSourceName");
    }

    [Fact]
    public void Invalid_backfill_and_storage_limits_are_rejected()
    {
        var original = ValidConfiguration();
        var configuration = new MvpConfiguration
        {
            ConfigurationVersion = original.ConfigurationVersion,
            Source = new SourceConfiguration
            {
                LaunchAdapter = original.Source.LaunchAdapter,
                PoolAdapter = original.Source.PoolAdapter,
                AdapterVersion = original.Source.AdapterVersion,
                LaunchLabParserVersion = original.Source.LaunchLabParserVersion,
                CpmmParserVersion = original.Source.CpmmParserVersion,
                ProgramIds = original.Source.ProgramIds,
                FixtureCoverageStartSlot = original.Source.FixtureCoverageStartSlot,
                RpcSourceName = original.Source.RpcSourceName,
                BackfillMaximumSlotsPerCycle = 0
            },
            Storage = new StorageConfiguration
            {
                PartitionAheadMonths = 0
            },
            Theme = original.Theme,
            Risk = original.Risk
        };

        var errors = MvpConfigurationValidator.Validate(configuration);

        Assert.Contains(
            errors,
            error => error.Path == "source.backfillMaximumSlotsPerCycle");
        Assert.Contains(
            errors,
            error => error.Path == "storage.partitionAheadMonths");
    }

    [Fact]
    public void Invalid_sell_quote_probe_size_is_rejected()
    {
        var original = ValidConfiguration();
        var configuration = new MvpConfiguration
        {
            ConfigurationVersion = original.ConfigurationVersion,
            Source = original.Source,
            Radar = original.Radar,
            Storage = original.Storage,
            Theme = original.Theme,
            Risk = new RiskConfiguration
            {
                ModelVersion = original.Risk.ModelVersion,
                SellQuoteProbeReserveBasisPoints = 0
            }
        };

        var errors = MvpConfigurationValidator.Validate(configuration);

        Assert.Contains(
            errors,
            error =>
                error.Path == "risk.sellQuoteProbeReserveBasisPoints");
    }

    private static MvpConfiguration ValidConfiguration() => new()
    {
        ConfigurationVersion = "phase1-mvp-research-v1",
        Source = new SourceConfiguration
        {
            LaunchAdapter = "RaydiumLaunchLabV1",
            PoolAdapter = "RaydiumCpmmV1",
            AdapterVersion = "raydium-launchlab-cpmm-m0-v1",
            LaunchLabParserVersion = "raydium-launchlab-e7e0c96-v1",
            CpmmParserVersion = "raydium-cpmm-e7e0c96-v1",
            ProgramIds =
            [
                "LanMV9sAd7wArD4vJFi2qDdfnVhFxYSUg6eADduJ3uj",
                "CPMMoo8L3F4NbTegBCKVNunggL7H1ZpdTHKxQB5qKP1C"
            ],
            FixtureCoverageStartSlot = 339103624,
            RpcSourceName = "solana-public-development"
        },
        Theme = new ThemeConfiguration
        {
            ConfigurationVersion = "theme-rules-v1"
        },
        Risk = new RiskConfiguration
        {
            ModelVersion = "risk-rules-v1"
        }
    };
}

internal static class ConfigurationTestExtensions
{
    public static MvpConfiguration WithSource(
        this MvpConfiguration configuration,
        SourceConfiguration source) =>
        new()
        {
            ConfigurationVersion = configuration.ConfigurationVersion,
            FormalRun = configuration.FormalRun,
            Source = source,
            Radar = configuration.Radar,
            Storage = configuration.Storage,
            Theme = configuration.Theme,
            Risk = configuration.Risk
        };
}
