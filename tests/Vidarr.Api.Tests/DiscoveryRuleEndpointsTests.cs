using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vidarr.Api;
using Vidarr.Rules;

namespace Vidarr.Api.Tests;

public class DiscoveryRuleEndpointsTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly StubEngine _engine = new();

    public DiscoveryRuleEndpointsTests()
    {
        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddSingleton<IDiscoveryRuleEngine>(_engine);
            });
            web.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrDiscoveryRuleApi());
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
    public async Task EvaluateById_returns_result_when_rule_exists()
    {
        _engine.SetSingle(new DiscoveryEvaluationResult(7, "MyRule", Matched: 3, VideosMonitored: 2));
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/discoveryrule/evaluate/7"), null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<DiscoveryEvaluationDto>();
        dto!.RuleName.Should().Be("MyRule");
        dto.Matched.Should().Be(3);
        dto.VideosMonitored.Should().Be(2);
    }

    [Fact]
    public async Task EvaluateById_returns_404_when_rule_missing()
    {
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/discoveryrule/evaluate/9999"), null);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EvaluateAll_returns_engine_results_array()
    {
        _engine.SetAll([
            new(1, "A", 1, 1),
            new(2, "B", 0, 0),
        ]);
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/discoveryrule/evaluate-all"), null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await resp.Content.ReadFromJsonAsync<DiscoveryEvaluationDto[]>();
        dtos!.Should().HaveCount(2);
    }

    private sealed class StubEngine : IDiscoveryRuleEngine
    {
        private DiscoveryEvaluationResult? _single;
        private IReadOnlyList<DiscoveryEvaluationResult> _all = [];
        public void SetSingle(DiscoveryEvaluationResult r) { _single = r; _all = [r]; }
        public void SetAll(IReadOnlyList<DiscoveryEvaluationResult> rs) => _all = rs;
        public Task<DiscoveryEvaluationResult?> EvaluateAsync(int ruleId, CancellationToken ct) =>
            Task.FromResult(_single?.RuleId == ruleId ? _single : null);
        public Task<IReadOnlyList<DiscoveryEvaluationResult>> EvaluateAllAsync(CancellationToken ct) =>
            Task.FromResult(_all);
    }
}
