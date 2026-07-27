using CryptoIntelligence.Application.Ingestion;

namespace CryptoIntelligence.Worker;

public sealed class LoggingDiscoveryConnectionObserver(
    ILogger<LoggingDiscoveryConnectionObserver> logger)
    : IDiscoveryConnectionObserver
{
    public ValueTask ConnectedAsync(
        string source,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Solana WebSocket connected to {Source} at {Timestamp}",
            source,
            timestamp);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectedAsync(
        string source,
        string reason,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "Solana WebSocket disconnected from {Source} at {Timestamp}: {Reason}",
            source,
            timestamp,
            reason);
        return ValueTask.CompletedTask;
    }
}
