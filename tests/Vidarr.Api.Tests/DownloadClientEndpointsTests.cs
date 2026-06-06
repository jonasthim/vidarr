using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vidarr.Api;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;

namespace Vidarr.Api.Tests;

public class DownloadClientEndpointsTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;

    public DownloadClientEndpointsTests()
    {
        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddSingleton<IDownloadClientFactory>(new StubFactory("QBittorrent", DownloadProtocol.Torrent, true));
                s.AddSingleton<IDownloadClientFactory>(new StubFactory("Transmission", DownloadProtocol.Torrent, false));
            });
            web.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrDownloadClientApi());
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
    public async Task Download_client_schema_lists_factories()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/downloadclient/schema"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var schemas = await resp.Content.ReadFromJsonAsync<DownloadClientSchemaDto[]>();
        schemas.Should().NotBeNull();
        schemas!.Should().HaveCount(2);
        schemas!.Select(s => s.Implementation).Should().BeEquivalentTo(["QBittorrent", "Transmission"]);
    }

    [Fact]
    public async Task Download_client_test_returns_factory_pass()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/downloadclient/test"),
            new DownloadClientTestRequest("QBittorrent", "{}"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<DownloadClientTestResultDto>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Download_client_test_returns_factory_fail()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/downloadclient/test"),
            new DownloadClientTestRequest("Transmission", "{}"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<DownloadClientTestResultDto>();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Download_client_test_unknown_impl_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/downloadclient/test"),
            new DownloadClientTestRequest("Garbage", "{}"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed class StubFactory : IDownloadClientFactory
    {
        private readonly bool _testPasses;
        public StubFactory(string impl, DownloadProtocol protocol, bool testPasses)
        {
            Implementation = impl;
            Protocol = protocol;
            _testPasses = testPasses;
        }
        public string Implementation { get; }
        public string DisplayName => Implementation;
        public DownloadProtocol Protocol { get; }
        public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } =
            [new("baseUrl", "URL", "url", true, null)];
        public IDownloadClient Create(int id, string name, string settingsJson) => new StubClient(_testPasses);
    }

    private sealed class StubClient : IDownloadClient
    {
        private readonly bool _passes;
        public StubClient(bool passes) { _passes = passes; }
        public int Id => 0;
        public string Name => "stub";
        public DownloadProtocol Protocol => DownloadProtocol.Torrent;
        public Task<DownloadClientItemId> DownloadAsync(RemoteRelease r, CancellationToken ct) => Task.FromResult(new DownloadClientItemId("x"));
        public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DownloadClientItem>>([]);
        public Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct) => Task.CompletedTask;
        public Task<DownloadClientTestResult> TestAsync(CancellationToken ct) =>
            Task.FromResult(_passes ? new DownloadClientTestResult(true, "ok") : new DownloadClientTestResult(false, "nope"));
    }
}
