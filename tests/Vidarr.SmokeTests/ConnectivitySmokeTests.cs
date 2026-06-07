using System.Net;

namespace Vidarr.SmokeTests;

/// <summary>Probes the absolute minimum: process boots, status endpoint responds.</summary>
[Collection(nameof(SmokeCollection))]
public class ConnectivitySmokeTests
{
    private readonly SmokeFactory _factory;
    public ConnectivitySmokeTests(SmokeFactory factory) { _factory = factory; }

    [Fact]
    public async Task System_status_returns_200_without_auth()
    {
        using var client = _factory.CreateClient();
        var resp = await client.GetAsync(new Uri("/api/v1/system/status", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"version\"");
    }

    [Fact]
    public async Task Health_endpoint_responds_with_empty_status_on_boot()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "smoke-key");
        var resp = await client.GetAsync(new Uri("/api/v1/health", UriKind.Relative));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("\"issues\":");
    }
}
