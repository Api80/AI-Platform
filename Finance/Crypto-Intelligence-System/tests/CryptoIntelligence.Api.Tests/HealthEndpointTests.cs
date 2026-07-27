using System.Net;
using System.Net.Http.Json;
using CryptoIntelligence.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CryptoIntelligence.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<CryptoApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(CryptoApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Live_health_does_not_depend_on_postgresql()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-ID", "api-test-correlation");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "api-test-correlation",
            response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task System_status_exposes_configuration_identity()
    {
        var response = await _client.GetFromJsonAsync<SystemStatusResponse>(
            "/api/v1/system/status");

        Assert.NotNull(response);
        Assert.Equal("M3 Theme and Minimal Risk", response.Milestone);
        Assert.Equal("phase1-mvp-research-v1", response.ConfigurationVersion);
        Assert.Equal(64, response.ConfigurationHash.Length);
    }

    [Fact]
    public async Task Ready_health_reports_unavailable_database()
    {
        using var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Radar_rejects_unknown_candidate_status_before_database_query()
    {
        using var response = await _client.GetAsync(
            "/api/v1/radar/candidates?status=not-a-state");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Intelligence_evaluation_does_not_depend_on_postgresql()
    {
        var now = DateTimeOffset.Parse("2026-07-28T00:01:00Z");
        var request = new IntelligenceEvaluationRequest(
            "Example AI",
            "EAI",
            now.AddSeconds(-60),
            now,
            HasUsableLiquidity: true,
            MarketAsOfTime: now.AddSeconds(-1),
            QuoteReserveRaw: 10_000,
            EntryPriceImpactBasisPoints: 100,
            LiquidityDropBasisPoints: 0,
            MintAuthorityEnabled: false,
            FreezeAuthorityEnabled: false,
            AdapterAuthorityRisk: false,
            CreatorHoldingBasisPoints: 100,
            Top10HoldingBasisPoints: 1_000,
            PoolVersionSupported: true,
            IsFinalized: true,
            IsReconciled: true,
            SellQuote: new SellQuoteEvidenceRequest(
                "Available",
                InputBaseAmount: 100,
                OutputQuoteAmount: 10,
                PriceImpactBasisPoints: 100,
                AsOfTime: now.AddSeconds(-1),
                AdapterVersion: "adapter-v1",
                FailureReason: null));

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/intelligence/evaluate",
            request);
        var result = await response.Content
            .ReadFromJsonAsync<IntelligenceEvaluationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.False(result.Risk.HardReject);
        Assert.Equal("Eligible", result.Candidate.Status);
    }
}

public sealed class CryptoApiFactory : WebApplicationFactory<Program>
{
    private const string UnavailableConnection =
        "Host=127.0.0.1;Port=65432;Database=unavailable;" +
        "Username=postgres;Timeout=1;Command Timeout=1";

    public CryptoApiFactory()
    {
        Environment.SetEnvironmentVariable("CRYPTO_DB_CONNECTION", UnavailableConnection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] =
                            UnavailableConnection
                    }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("CRYPTO_DB_CONNECTION", null);
    }
}
