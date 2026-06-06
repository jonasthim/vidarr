using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Api;
using Vidarr.Catalog;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.Indexers;

namespace Vidarr.Api.Tests;

public class ReleaseEndpointsTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;

    private readonly List<ReleaseInfo> _stubReleases = [];
    private readonly StubReleaseSearchService _search;

    public ReleaseEndpointsTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _search = new StubReleaseSearchService(_stubReleases);

        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddDbContext<VidarrDbContext>(o => o.UseSqlite(_conn));
                s.AddScoped<IArtistRepository, ArtistRepository>();
                s.AddScoped<IMusicVideoRepository, MusicVideoRepository>();
                s.AddSingleton<IReleaseSearchService>(_search);
                s.AddSingleton<IIndexerFactory>(new StubIndexerFactory("Newznab", true));
                s.AddSingleton<IIndexerFactory>(new StubIndexerFactory("Torznab", false));
            });
            web.Configure(app =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<VidarrDbContext>().Database.EnsureCreated();
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrReleaseApi());
            });
        }).Start();

        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Release_search_by_query_returns_releases_envelope()
    {
        _stubReleases.Add(new ReleaseInfo("Sample R", new Uri("https://x.example/r"), null, 100,
            DateTimeOffset.UtcNow, null, 1, 0, DownloadProtocol.Torrent, "Indexer", "6030", new Dictionary<string, string>()));
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/release?query=daft"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await resp.Content.ReadFromJsonAsync<ReleaseSearchResponse>();
        env.Should().NotBeNull();
        env!.Releases.Should().ContainSingle();
        env.IndexersQueried.Should().Be(1);
    }

    [Fact]
    public async Task Release_search_without_criteria_returns_400()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/release"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Release_search_by_artist_and_video_404s_when_unknown()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/release?artistId=999&musicVideoId=999"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Release_search_by_artist_and_video_resolves_criteria()
    {
        using (var scope = _host.Services.CreateScope())
        {
            var artistRepo = scope.ServiceProvider.GetRequiredService<IArtistRepository>();
            var videoRepo = scope.ServiceProvider.GetRequiredService<IMusicVideoRepository>();
            var a = await artistRepo.AddAsync(new Artist { Name = "Daft Punk", SortName = "Daft Punk" }, default);
            await videoRepo.AddAsync(new MusicVideo { ArtistId = a.Id, Title = "Around the World", Year = 1997 }, default);
        }
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/release?artistId=1&musicVideoId=1"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        _search.LastCriteria!.ArtistName.Should().Be("Daft Punk");
        _search.LastCriteria.Title.Should().Be("Around the World");
        _search.LastCriteria.Year.Should().Be(1997);
    }

    [Fact]
    public async Task Indexer_schema_returns_each_registered_factory()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/indexer/schema"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var schemas = await resp.Content.ReadFromJsonAsync<IndexerSchemaDto[]>();
        schemas.Should().NotBeNull();
        schemas!.Should().HaveCount(2);
        schemas!.Select(s => s.Implementation).Should().BeEquivalentTo(["Newznab", "Torznab"]);
    }

    [Fact]
    public async Task Indexer_test_unknown_implementation_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/indexer/test"),
            new IndexerTestRequest("Garbage", "{}"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Indexer_test_returns_factory_result()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/indexer/test"),
            new IndexerTestRequest("Newznab", "{}"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<IndexerTestResultDto>();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Indexer_test_failing_factory_returns_success_false()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/indexer/test"),
            new IndexerTestRequest("Torznab", "{}"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<IndexerTestResultDto>();
        result!.Success.Should().BeFalse();
    }

    private sealed class StubReleaseSearchService : IReleaseSearchService
    {
        private readonly IReadOnlyList<ReleaseInfo> _stub;
        public IndexerSearchCriteria? LastCriteria { get; private set; }
        public StubReleaseSearchService(IReadOnlyList<ReleaseInfo> stub) { _stub = stub; }
        public Task<ReleaseSearchResult> SearchAsync(IndexerSearchCriteria c, CancellationToken ct)
        {
            LastCriteria = c;
            return Task.FromResult(new ReleaseSearchResult(_stub, [], 1));
        }
        public Task<ReleaseSearchResult> RssSyncAsync(CancellationToken ct) =>
            Task.FromResult(new ReleaseSearchResult(_stub, [], 1));
    }

    private sealed class StubIndexerFactory : IIndexerFactory
    {
        private readonly bool _testPasses;
        public StubIndexerFactory(string impl, bool testPasses) { Implementation = impl; _testPasses = testPasses; }
        public string Implementation { get; }
        public string DisplayName => Implementation;
        public IReadOnlyList<IndexerFieldSchema> SettingsSchema { get; } =
            [new("baseUrl", "URL", "url", true, null)];
        public IIndexer Create(int id, string name, string settingsJson) =>
            new StubIndexer(id, name, _testPasses);
    }

    private sealed class StubIndexer : IIndexer
    {
        private readonly bool _testPasses;
        public StubIndexer(int id, string name, bool testPasses) { Id = id; Name = name; _testPasses = testPasses; }
        public int Id { get; }
        public string Name { get; }
        public DownloadProtocol Protocol => DownloadProtocol.Usenet;
        public bool SupportsRss => true;
        public bool SupportsSearch => true;
        public Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria c, CancellationToken ct) => Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
        public Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
        public Task<IndexerTestResult> TestAsync(CancellationToken ct) =>
            Task.FromResult(_testPasses
                ? new IndexerTestResult(true, "OK")
                : new IndexerTestResult(false, "bad config"));
    }
}
