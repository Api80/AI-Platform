using System.Data;
using CryptoIntelligence.Application.Ingestion;
using Microsoft.EntityFrameworkCore;

namespace CryptoIntelligence.Infrastructure.Persistence;

public sealed class PostgresIngestionOperationsQuery(
    CryptoIntelligenceDbContext context)
    : IIngestionOperationsQuery
{
    public async Task<IngestionCapacityReport> GetCapacityReportAsync(
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                c.relname,
                COALESCE(s.n_live_tup, 0)::bigint AS estimated_rows,
                pg_relation_size(c.oid)::bigint AS data_bytes,
                pg_indexes_size(c.oid)::bigint AS index_bytes,
                pg_total_relation_size(c.oid)::bigint AS total_bytes,
                EXISTS (
                    SELECT 1
                    FROM pg_partitioned_table p
                    WHERE p.partrelid = c.oid
                ) AS is_partitioned
            FROM pg_class c
            LEFT JOIN pg_stat_user_tables s ON s.relid = c.oid
            WHERE c.relname IN (
                'raw_blockchain_events',
                'normalized_domain_events',
                'swap_events',
                'liquidity_events',
                'market_snapshots',
                'feature_snapshots')
              AND c.relkind IN ('r', 'p')
            ORDER BY c.relname
            """;
        var values = new List<StorageTableCapacity>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(new StorageTableCapacity(
                    reader.GetString(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetInt64(3),
                    reader.GetInt64(4),
                    reader.GetBoolean(5)));
            }
        }

        await using var activity = connection.CreateCommand();
        activity.CommandText = """
            SELECT
                (
                    SELECT COUNT(*)::bigint
                    FROM raw_blockchain_events
                    WHERE event_time >= NOW() - INTERVAL '24 hours'
                ),
                (
                    SELECT COALESCE(SUM(pg_column_size(value)), 0)::bigint
                    FROM raw_blockchain_events value
                    WHERE event_time >= NOW() - INTERVAL '24 hours'
                ),
                (
                    SELECT COUNT(*)::bigint
                    FROM swap_events
                    WHERE event_time >= NOW() - INTERVAL '24 hours'
                ),
                (
                    SELECT COUNT(*)::bigint
                    FROM market_snapshots
                    WHERE as_of_time >= NOW() - INTERVAL '24 hours'
                ),
                (SELECT MIN(event_time) FROM raw_blockchain_events),
                (SELECT MAX(event_time) FROM raw_blockchain_events)
            """;
        await using var activityReader =
            await activity.ExecuteReaderAsync(cancellationToken);
        await activityReader.ReadAsync(cancellationToken);
        return new IngestionCapacityReport(
            DateTimeOffset.UtcNow,
            values,
            activityReader.GetInt64(0),
            activityReader.GetInt64(1),
            activityReader.GetInt64(2),
            activityReader.GetInt64(3),
            activityReader.IsDBNull(4)
                ? null
                : activityReader.GetFieldValue<DateTimeOffset>(4),
            activityReader.IsDBNull(5)
                ? null
                : activityReader.GetFieldValue<DateTimeOffset>(5));
    }
}
