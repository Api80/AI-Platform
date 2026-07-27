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
        Assert.Equal("M2 New Token Radar", response.Milestone);
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
