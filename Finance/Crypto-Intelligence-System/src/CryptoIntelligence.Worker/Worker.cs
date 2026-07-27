using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Domain.Ingestion;
using System.Text.Json;

namespace CryptoIntelligence.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    ConfigurationSnapshot configurationSnapshot,
    MvpConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IEnumerable<ISolanaDiscoverySource> discoverySources,
    IEnumerable<ISolanaBackfillSource> backfillSources)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Crypto Intelligence worker started with configuration {ConfigurationVersion} " +
            "and hash {ConfigurationHash}",
            configurationSnapshot.ConfigurationVersion,
            configurationSnapshot.ConfigurationHash);

        var discovery = discoverySources.SingleOrDefault();
        var backfill = backfillSources.SingleOrDefault();
        if (discovery is null || backfill is null)
        {
            logger.LogWarning(
                "Solana ingestion is disabled. Set SOLANA_RPC_WS_URL and " +
                "SOLANA_RPC_HTTP_URL to enable public-chain ingestion.");
            await HeartbeatAsync(stoppingToken);
            return;
        }

        await Task.WhenAll(
            DiscoverAsync(discovery, stoppingToken),
            DispatchAsync(stoppingToken),
            ReconcileAsync(stoppingToken));
    }

    private async Task DiscoverAsync(
        ISolanaDiscoverySource discovery,
        CancellationToken cancellationToken)
    {
        await foreach (var notification in discovery.DiscoverAsync(cancellationToken))
        {
            var identity = new RawEventIdentity(
                "Solana",
                "mainnet-beta",
                notification.Signature,
                -1,
                null,
                "SolanaSignatureDiscovered",
                0,
                "solana-discovery-v1");
            var input = new RawBlockchainEventInput(
                identity,
                notification.Slot,
                null,
                notification.ProgramId,
                notification.ObservedAt,
                notification.ObservedAt,
                configuration.Source.DiscoveryCommitment,
                CanonicalStatus.Observed,
                configuration.Source.RpcSourceName,
                JsonSerializer.Serialize(notification),
                notification.Signature);
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IRawEventStore>();
            var persisted = await store.PersistAsync(input, cancellationToken);
            var reconciliationStore = scope.ServiceProvider
                .GetRequiredService<IIngestionReconciliationStore>();
            await reconciliationStore.RecordRealtimeObservationAsync(
                new IngestionCheckpointKey(
                    "Solana",
                    "mainnet-beta",
                    configuration.Source.RpcSourceName,
                    notification.ProgramId),
                notification.Slot,
                notification.ObservedAt,
                cancellationToken);
            logger.LogInformation(
                "Solana discovery {Signature} persisted as {EventId}; inserted={Inserted}",
                notification.Signature,
                persisted.EventId,
                persisted.Inserted);
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var startSlot = configuration.Source.HistoricalRunStartSlot ??
                        configuration.Source.FixtureCoverageStartSlot;
        var initialThroughSlot = startSlot == 0 ? 0 : startSlot - 1;
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(
                configuration.Source.ReconciliationIntervalSeconds));
        do
        {
            foreach (var programId in configuration.Source.ProgramIds)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var reconciliation = scope.ServiceProvider
                        .GetRequiredService<SolanaBackfillReconciliationService>();
                    var result = await reconciliation.RunCycleAsync(
                        programId,
                        initialThroughSlot,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                    logger.LogInformation(
                        "Solana reconciliation {ProgramId} covered ({FromSlot}, {ToSlot}] " +
                        "with {SignatureCount} signatures, {GapCount} gaps and reconciled " +
                        "watermark {ReconciledSlot}",
                        result.ProgramId,
                        result.FromExclusive,
                        result.ToInclusive,
                        result.SignatureCount,
                        result.GapCount,
                        result.Watermarks.ReconciledThroughSlot);
                }
                catch (Exception exception) when (
                    exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "Solana reconciliation failed for {ProgramId}",
                        programId);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var workerId = $"{Environment.MachineName}-{Environment.ProcessId}";
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider
                .GetRequiredService<DurableRawEventDispatcher>();
            await dispatcher.DispatchBatchAsync(
                workerId,
                batchSize: 50,
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(30),
                maximumRetries: 5,
                cancellationToken);
        }
    }

    private async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            logger.LogInformation("M2 worker heartbeat; ingestion disabled");
        }
    }
}
