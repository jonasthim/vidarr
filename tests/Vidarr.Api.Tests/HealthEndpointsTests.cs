using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Api;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.EventBus;
using Vidarr.Health;

namespace Vidarr.Api.Tests;

public class HealthEndpointsTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly StubCheck _stub = new();

    public HealthEndpointsTests()
    {
        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddSingleton<IEventBus>(new InProcessEventBus(NullLogger<InProcessEventBus>.Instance));
                s.AddSingleton<IHealthCheck>(_stub);
                s.AddSingleton<IHealthMonitor>(sp => new HealthMonitor(
                    sp.GetServices<IHealthCheck>(),
                    sp.GetRequiredService<IEventBus>(),
                    NullLogger<HealthMonitor>.Instance));
            });
            web.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrHealthApi());
            });
        }).Start();
        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Get_health_returns_empty_status_before_first_run()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/health"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<HealthStatusDto>();
        dto.Should().NotBeNull();
        dto!.Issues.Should().BeEmpty();
        dto.LastRun.Should().BeNull();
    }

    [Fact]
    public async Task Post_health_run_surfaces_issues()
    {
        _stub.NextIssues = [new HealthIssue(
            new HealthIssueId("Stub", "src"), HealthSeverity.Warning, "broken")];

        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/health/run"), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<HealthStatusDto>();
        dto.Should().NotBeNull();
        dto!.Issues.Should().ContainSingle(i =>
            i.CheckName == "Stub" && i.Source == "src" && i.Severity == "Warning" && i.Message == "broken");
        dto.LastRun.Should().NotBeNull();
    }

    private sealed class StubCheck : IHealthCheck
    {
        public string Name => "Stub";
        public IReadOnlyList<HealthIssue> NextIssues { get; set; } = [];
        public Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct) => Task.FromResult(NextIssues);
    }
}
