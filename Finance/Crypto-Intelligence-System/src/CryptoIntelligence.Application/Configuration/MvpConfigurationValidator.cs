using CryptoIntelligence.Domain.Common;

namespace CryptoIntelligence.Application.Configuration;

public sealed record ConfigurationError(string Path, string Message);

public static class MvpConfigurationValidator
{
    public static IReadOnlyList<ConfigurationError> Validate(MvpConfiguration? configuration)
    {
        if (configuration is null)
        {
            return [new ConfigurationError(MvpConfiguration.SectionName, "Configuration section is required.")];
        }

        var errors = new List<ConfigurationError>();
        Required(configuration.ConfigurationVersion, "configurationVersion", errors);
        Required(configuration.Source.LaunchAdapter, "source.launchAdapter", errors);
        Required(configuration.Source.PoolAdapter, "source.poolAdapter", errors);
        Required(configuration.Source.AdapterVersion, "source.adapterVersion", errors);
        Required(configuration.Source.LaunchLabParserVersion, "source.launchLabParserVersion", errors);
        Required(configuration.Source.CpmmParserVersion, "source.cpmmParserVersion", errors);
        Required(configuration.Source.RpcSourceName, "source.rpcSourceName", errors);

        if (configuration.Source.ProgramIds.Count == 0)
        {
            errors.Add(new ConfigurationError("source.programIds", "At least one ProgramId is required."));
        }

        foreach (var (programId, index) in configuration.Source.ProgramIds.Select((value, index) => (value, index)))
        {
            try
            {
                _ = new ProgramId(programId);
            }
            catch (ArgumentException exception)
            {
                errors.Add(new ConfigurationError(
                    $"source.programIds[{index}]",
                    exception.Message));
            }
        }

        if (configuration.Source.FixtureCoverageStartSlot == 0)
        {
            errors.Add(new ConfigurationError(
                "source.fixtureCoverageStartSlot",
                "Fixture coverage start slot must be greater than zero."));
        }

        if (configuration.Source.BackfillMaximumSlotsPerCycle <= 0)
        {
            errors.Add(new ConfigurationError(
                "source.backfillMaximumSlotsPerCycle",
                "Backfill slot limit must be greater than zero."));
        }

        if (configuration.Source.BackfillMaximumSignaturesPerCycle <= 0)
        {
            errors.Add(new ConfigurationError(
                "source.backfillMaximumSignaturesPerCycle",
                "Backfill signature limit must be greater than zero."));
        }

        if (configuration.Source.ReconciliationIntervalSeconds <= 0)
        {
            errors.Add(new ConfigurationError(
                "source.reconciliationIntervalSeconds",
                "Reconciliation interval must be greater than zero."));
        }

        if (!string.Equals(
                configuration.Source.StrategyCommitment,
                "finalized",
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ConfigurationError(
                "source.strategyCommitment",
                "Strategy commitment must be finalized."));
        }

        if (configuration.Radar.MinimumObservationSeconds <= 0)
        {
            errors.Add(new ConfigurationError(
                "radar.minimumObservationSeconds",
                "Minimum observation time must be greater than zero."));
        }

        if (configuration.Radar.FeatureWindowsSeconds.Count == 0 ||
            configuration.Radar.FeatureWindowsSeconds.Any(value => value <= 0))
        {
            errors.Add(new ConfigurationError(
                "radar.featureWindowsSeconds",
                "At least one positive feature window is required."));
        }

        if (configuration.Radar.MaximumEntryAgeSeconds >
            configuration.Radar.MaximumCandidateAgeSeconds)
        {
            errors.Add(new ConfigurationError(
                "radar.maximumEntryAgeSeconds",
                "Entry age cannot exceed candidate age."));
        }

        if (configuration.Radar.MaximumMarketDataAgeSeconds <
            configuration.Radar.MarketSnapshotIntervalSeconds)
        {
            errors.Add(new ConfigurationError(
                "radar.maximumMarketDataAgeSeconds",
                "Market data age cannot be shorter than the snapshot interval."));
        }

        if (configuration.Storage.PartitionAheadMonths <= 0)
        {
            errors.Add(new ConfigurationError(
                "storage.partitionAheadMonths",
                "At least one future partition month is required."));
        }

        if (configuration.Storage.RebuildableHotRetentionDays <= 0 ||
            configuration.Storage.OperationalRetentionDays <= 0 ||
            configuration.Storage.CapacityReviewMinimumDays <= 0)
        {
            errors.Add(new ConfigurationError(
                "storage",
                "Retention and capacity review durations must be positive."));
        }

        if (configuration.FormalRun)
        {
            if (configuration.Source.HistoricalRunStartSlot is null or 0)
            {
                errors.Add(new ConfigurationError(
                    "source.historicalRunStartSlot",
                    "Formal runs require an explicit historical start slot."));
            }

            Required(
                configuration.Source.FallbackRpcSourceName,
                "source.fallbackRpcSourceName",
                errors);

            if (!configuration.Source.RequireReconciledData)
            {
                errors.Add(new ConfigurationError(
                    "source.requireReconciledData",
                    "Formal runs require reconciled data."));
            }
        }

        return errors;
    }

    public static void ThrowIfInvalid(MvpConfiguration? configuration)
    {
        var errors = Validate(configuration);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidMvpConfigurationException(errors);
    }

    private static void Required(
        string? value,
        string path,
        ICollection<ConfigurationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ConfigurationError(path, "Value is required."));
        }
    }
}

public sealed class InvalidMvpConfigurationException(IReadOnlyList<ConfigurationError> errors)
    : Exception(
        "Crypto Intelligence configuration is invalid: " +
        string.Join("; ", errors.Select(error => $"{error.Path}: {error.Message}")))
{
    public IReadOnlyList<ConfigurationError> Errors { get; } = errors;
}
