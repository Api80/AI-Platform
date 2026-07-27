using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoIntelligence.Application.Configuration;

public sealed record ConfigurationSnapshot(
    string ConfigurationVersion,
    string ConfigurationHash,
    string CanonicalJson,
    DateTimeOffset CreatedAtUtc);

public static class ConfigurationSnapshotFactory
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static ConfigurationSnapshot Create(
        MvpConfiguration configuration,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Snapshot timestamp must be UTC.", nameof(createdAtUtc));
        }

        MvpConfigurationValidator.ThrowIfInvalid(configuration);
        var canonicalJson = JsonSerializer.Serialize(configuration, CanonicalOptions);
        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)))
            .ToLowerInvariant();

        return new ConfigurationSnapshot(
            configuration.ConfigurationVersion,
            hash,
            canonicalJson,
            createdAtUtc);
    }
}
