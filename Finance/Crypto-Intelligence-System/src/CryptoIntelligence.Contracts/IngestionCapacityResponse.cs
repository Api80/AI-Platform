namespace CryptoIntelligence.Contracts;

public sealed record IngestionCapacityTableResponse(
    string TableName,
    long EstimatedRows,
    long DataBytes,
    long IndexBytes,
    long TotalBytes,
    bool IsPartitioned);

public sealed record IngestionCapacityResponse(
    DateTimeOffset GeneratedAt,
    long TotalBytes,
    int CapacityReviewMinimumDays,
    int PartitionAheadMonths,
    int RebuildableHotRetentionDays,
    int OperationalRetentionDays,
    long EventsLast24Hours,
    long RawBytesLast24Hours,
    long SwapsLast24Hours,
    long MarketSnapshotsLast24Hours,
    DateTimeOffset? OldestRawEventTime,
    DateTimeOffset? NewestRawEventTime,
    IReadOnlyList<IngestionCapacityTableResponse> Tables);
