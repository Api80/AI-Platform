namespace CryptoIntelligence.Application.Configuration;

public sealed class MvpConfiguration
{
    public const string SectionName = "CryptoIntelligence";

    public string ConfigurationVersion { get; init; } = string.Empty;

    public bool FormalRun { get; init; }

    public SourceConfiguration Source { get; init; } = new();

    public RadarConfiguration Radar { get; init; } = new();
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
}

public sealed class RadarConfiguration
{
    public int MinimumObservationSeconds { get; init; } = 30;

    public int MaximumCandidateAgeSeconds { get; init; } = 600;

    public int MaximumEntryAgeSeconds { get; init; } = 300;

    public int MarketSnapshotIntervalSeconds { get; init; } = 5;

    public int MaximumMarketDataAgeSeconds { get; init; } = 5;
}
