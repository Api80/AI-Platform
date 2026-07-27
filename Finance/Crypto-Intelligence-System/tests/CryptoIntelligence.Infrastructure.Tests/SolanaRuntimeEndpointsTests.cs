using CryptoIntelligence.Application.Configuration;
using CryptoIntelligence.Infrastructure.Solana;

namespace CryptoIntelligence.Infrastructure.Tests;

public sealed class SolanaRuntimeEndpointsTests
{
    [Fact]
    public void Development_without_endpoints_disables_ingestion()
    {
        var endpoints = SolanaRuntimeEndpoints.Create(
            Configuration(formalRun: false),
            null,
            null,
            null);

        Assert.Null(endpoints);
    }

    [Fact]
    public void Formal_run_requires_fallback_endpoint()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SolanaRuntimeEndpoints.Create(
                Configuration(formalRun: true),
                "wss://primary.example/ws",
                "https://primary.example/rpc",
                null));

        Assert.Contains("FALLBACK", exception.Message);
    }

    [Fact]
    public void Primary_and_fallback_must_be_distinct()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SolanaRuntimeEndpoints.Create(
                Configuration(formalRun: true),
                "wss://primary.example/ws",
                "https://primary.example/rpc",
                "https://primary.example/rpc"));

        Assert.Contains("distinct", exception.Message);
    }

    [Fact]
    public void Formal_distinct_endpoints_are_accepted()
    {
        var endpoints = SolanaRuntimeEndpoints.Create(
            Configuration(formalRun: true),
            "wss://primary.example/ws",
            "https://primary.example/rpc",
            "https://fallback.example/rpc");

        Assert.NotNull(endpoints);
        Assert.Equal("fallback.example", endpoints.FallbackHttp!.Host);
    }

    private static MvpConfiguration Configuration(bool formalRun) => new()
    {
        FormalRun = formalRun
    };
}
