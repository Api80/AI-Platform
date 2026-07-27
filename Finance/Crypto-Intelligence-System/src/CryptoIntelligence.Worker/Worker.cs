using CryptoIntelligence.Application.Configuration;

namespace CryptoIntelligence.Worker;

public sealed class Worker(
    ILogger<Worker> logger,
    ConfigurationSnapshot configurationSnapshot)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Crypto Intelligence worker started with configuration {ConfigurationVersion} " +
            "and hash {ConfigurationHash}",
            configurationSnapshot.ConfigurationVersion,
            configurationSnapshot.ConfigurationHash);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            logger.LogInformation("M1 foundation worker heartbeat");
        }
    }
}
