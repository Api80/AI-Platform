namespace CryptoIntelligence.Contracts;

public sealed record SystemStatusResponse(
    string Service,
    string Milestone,
    string ConfigurationVersion,
    string ConfigurationHash,
    DateTimeOffset UtcTime);
