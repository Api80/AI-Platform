namespace CryptoIntelligence.Application.Configuration;

public sealed class MvpConfiguration
{
    public const string SectionName = "CryptoIntelligence";

    public string ConfigurationVersion { get; init; } = string.Empty;

    public bool FormalRun { get; init; }

    public SourceConfiguration Source { get; init; } = new();

    public RadarConfiguration Radar { get; init; } = new();

    public StorageConfiguration Storage { get; init; } = new();

    public ThemeConfiguration Theme { get; init; } = new();

    public RiskConfiguration Risk { get; init; } = new();

    public M3AcceptanceConfiguration Acceptance { get; init; } = new();
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

public sealed class ThemeConfiguration
{
    public string Mode { get; init; } = "KeywordRules";

    public IReadOnlyList<string> HotKeywords { get; init; } = [];

    public IReadOnlyList<string> BlockedKeywords { get; init; } = [];

    public bool RequiredThemeMatch { get; init; }

    public bool CaseInsensitive { get; init; } = true;

    public bool NormalizeWhitespace { get; init; } = true;

    public DateTimeOffset? ThemeValidUntil { get; init; }

    public string ConfigurationVersion { get; init; } = string.Empty;
}

public sealed class RiskConfiguration
{
    public string ModelVersion { get; init; } = string.Empty;

    public int ScoreMinimum { get; init; }

    public int ScoreMaximum { get; init; } = 100;

    public int? MaximumAllowedRiskScore { get; init; }

    public int SellQuoteProbeReserveBasisPoints { get; init; } = 100;

    public HardRejectConfiguration HardReject { get; init; } = new();
}

public sealed class HardRejectConfiguration
{
    public bool RequireSellQuote { get; init; } = true;

    public bool RejectUnsupportedPoolVersion { get; init; } = true;

    public bool RejectStaleMarketState { get; init; } = true;

    public bool RejectNonFinalizedForFormalRun { get; init; } = true;

    public bool RejectNonReconciledForFormalRun { get; init; } = true;

    public bool RejectMintAuthorityRisk { get; init; } = true;

    public bool RejectFreezeAuthorityRisk { get; init; } = true;

    public decimal? MinimumQuoteReserveRaw { get; init; }

    public int? MaximumLiquidityDropBasisPoints { get; init; }

    public int? MaximumCreatorHoldingBasisPoints { get; init; }

    public int? MaximumTop10HoldingBasisPoints { get; init; }

    public int MaximumEntryPriceImpactBasisPoints { get; init; } = 1_000;

    public int MaximumMarketDataAgeSeconds { get; init; } = 5;
}

public sealed class M3AcceptanceConfiguration
{
    public int MinimumRunHours { get; init; } = 168;

    public int MinimumAutomatedAssessmentAttempts { get; init; } = 1;

    public int MinimumTerminalCoverageBasisPoints { get; init; } = 9_500;

    public bool RequireFallbackExercise { get; init; } = true;
}
