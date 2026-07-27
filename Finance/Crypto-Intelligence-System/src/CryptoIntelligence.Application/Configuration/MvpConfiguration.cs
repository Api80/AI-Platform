namespace CryptoIntelligence.Application.Configuration;

public sealed class MvpConfiguration
{
    public const string SectionName = "CryptoIntelligence";

    public string ConfigurationVersion { get; init; } = string.Empty;

    public bool FormalRun { get; init; }

    public SourceConfiguration Source { get; init; } = new();

    public RadarConfiguration Radar { get; init; } = new();

    public StorageConfiguration Storage { get; init; } = new();
}

public sealed class SourceConfiguration
{
    public string LaunchAdapter { get; init; } = string.Empty;

    public string PoolAdapter { get; init; } = string.Empty;

    public string AdapterVersion { get; init; } = string.Empty;

    public string LaunchLabParserVersion { get; init; } = string.Empty;

    public string CpmmParserVersion { get; init; } = string.Empty;

    public IReadOnlyList<string> ProgramIds { get; init; } = [];

    public ulong FixtureCoverageStartSlot { get; init; }

    public ulong? HistoricalRunStartSlot { get; init; }

    public string DiscoveryCommitment { get; init; } = "confirmed";

    public string StrategyCommitment { get; init; } = "finalized";

    public bool RequireReconciledData { get; init; } = true;

    public string RpcSourceName { get; init; } = string.Empty;

    public string? FallbackRpcSourceName { get; init; }

    public int BackfillMaximumSlotsPerCycle { get; init; } = 256;

    public int BackfillMaximumSignaturesPerCycle { get; init; } = 1_000;

    public int ReconciliationIntervalSeconds { get; init; } = 30;
}

public sealed class StorageConfiguration
{
    public int PartitionAheadMonths { get; init; } = 2;

    public int RebuildableHotRetentionDays { get; init; } = 180;

    public int OperationalRetentionDays { get; init; } = 30;

    public int CapacityReviewMinimumDays { get; init; } = 7;
}

public sealed class RadarConfiguration
{
    public IReadOnlyList<int> FeatureWindowsSeconds { get; init; } =
        [15, 30, 60, 180];

    public int MinimumObservationSeconds { get; init; } = 30;

    public int MaximumCandidateAgeSeconds { get; init; } = 600;

    public int MaximumEntryAgeSeconds { get; init; } = 300;

    public int MarketSnapshotIntervalSeconds { get; init; } = 5;

    public int MaximumMarketDataAgeSeconds { get; init; } = 5;
}
