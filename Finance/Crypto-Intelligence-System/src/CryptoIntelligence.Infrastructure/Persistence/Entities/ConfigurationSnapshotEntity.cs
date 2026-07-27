namespace CryptoIntelligence.Infrastructure.Persistence.Entities;

public sealed class ConfigurationSnapshotEntity
{
    public Guid Id { get; set; }

    public required string ConfigurationVersion { get; set; }

    public required string ConfigurationHash { get; set; }

    public required string CanonicalJson { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
