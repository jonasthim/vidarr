using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vidarr.Api;
using Vidarr.Catalog;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.Scheduler;
using Vidarr.Tests.Common;

namespace Vidarr.Api.Tests;

public class EndpointsTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly FakeMetadataProvider _metadata = new();
    private readonly StubIndexer _indexer = new();
    private readonly StubDownloadClient _downloadClient = new();
    private readonly StubCommandQueue _queue = new();

    public EndpointsTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();

        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddDbContext<VidarrDbContext>(o => o.UseSqlite(_conn));
                s.AddScoped<IArtistRepository, ArtistRepository>();
                s.AddScoped<IMusicVideoRepository, MusicVideoRepository>();
                s.AddSingleton<IMetadataProvider>(_metadata);
                s.AddSingleton<IIndexer>(_indexer);
                s.AddSingleton<IDownloadClient>(_downloadClient);
                s.AddSingleton<ICommandQueue>(_queue);
                s.AddSingleton<ISystemClock, FakeClock>();
            });
            web.Configure(app =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<VidarrDbContext>().Database.EnsureCreated();
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrApi());
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
    public async Task System_status_returns_payload()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/system/status"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<SystemStatusDto>();
        dto.Should().NotBeNull();
        dto!.Version.Should().Be("0.1.0");
    }

    [Fact]
    public async Task Artist_lookup_returns_provider_results()
    {
        _metadata.SearchResults =
        [
            new ArtistSearchResult("5489", "Daft Punk", null, null, "France", new Uri("https://example.com/x.png")),
        ];
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/artist/lookup"), new ArtistLookupRequest("daft"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await resp.Content.ReadFromJsonAsync<ArtistLookupResult[]>();
        results.Should().NotBeNull();
        results!.Should().HaveCount(1);
        results![0].Name.Should().Be("Daft Punk");
    }

    [Fact]
    public async Task Artist_lookup_with_empty_query_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/artist/lookup"), new ArtistLookupRequest(""));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_artist_persists_and_returns_201()
    {
        _metadata.ArtistDetails = new ArtistDetails(
            ProviderId: "5489", Name: "Daft Punk", SortName: "Daft Punk", Disambiguation: null,
            Aliases: [], Genres: ["electronic"], Country: "France",
            YearsActiveStart: 1993, YearsActiveEnd: 2021, Images: [],
            ExternalIds: new Dictionary<string, string> { ["imvdb"] = "5489" },
            YouTubeChannelIds: ["UCDaft"]);

        var req = new AddArtistRequest(Provider: "imvdb", ProviderId: "5489", RootFolderPath: "/library", QualityProfileId: 1, MonitorMode: MonitorMode.All);
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/artist"), req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<ArtistDto>();
        dto!.Name.Should().Be("Daft Punk");
        dto.RootFolderPath.Should().Be("/library");
    }

    [Fact]
    public async Task Add_artist_with_unknown_provider_returns_400()
    {
        var req = new AddArtistRequest("nonsense", "1", "/library", 1, MonitorMode.All);
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/artist"), req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_artist_twice_returns_409()
    {
        _metadata.ArtistDetails = new ArtistDetails(
            "1", "X", "X", null, [], [], null, null, null, [],
            new Dictionary<string, string> { ["imvdb"] = "1" }, []);
        var req = new AddArtistRequest("imvdb", "1", "/library", 1, MonitorMode.All);
        await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/artist"), req);
        var second = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/artist"), req);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_artist_by_id_404s_when_missing()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/artist/9999"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_music_videos_without_artist_id_returns_400()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/musicvideo"));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Command_endpoint_enqueues_artist_search()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/command"),
            new CommandRequest("ArtistSearch", ArtistId: 1, MusicVideoId: null));
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _queue.Enqueued.Should().ContainSingle().Which.Should().BeOfType<ArtistSearchCommand>();
    }

    [Fact]
    public async Task Command_endpoint_unknown_name_returns_400()
    {
        var resp = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/command"),
            new CommandRequest("Garbage", null, null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Queue_endpoint_returns_active_items()
    {
        _downloadClient.Items =
        [
            new DownloadClientItem(new DownloadClientItemId("abc"), "Test", 100, 50, DownloadItemStatus.Downloading, null, TimeSpan.FromSeconds(30), null),
        ];
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/queue"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Test").And.Contain("Downloading");
    }

    [Fact]
    public async Task Command_for_refresh_and_search_video_enqueues_corresponding_command()
    {
        var resp1 = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/command"),
            new CommandRequest("RefreshArtistMetadata", ArtistId: 1, MusicVideoId: null));
        var resp2 = await _client.PostAsJsonAsync(new Uri("http://localhost/api/v1/command"),
            new CommandRequest("SearchMusicVideo", ArtistId: null, MusicVideoId: 7));
        resp1.StatusCode.Should().Be(HttpStatusCode.Accepted);
        resp2.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _queue.Enqueued.Should().HaveCount(2);
        _queue.Enqueued[0].Should().BeOfType<RefreshArtistMetadataCommand>();
        _queue.Enqueued[1].Should().BeOfType<SearchMusicVideoCommand>();
    }
}

internal sealed class FakeMetadataProvider : IMetadataProvider
{
    public string Id => "imvdb";
    public IReadOnlyList<ArtistSearchResult> SearchResults { get; set; } = [];
    public ArtistDetails ArtistDetails { get; set; } = new(
        "0", "", "", null, [], [], null, null, null, [],
        new Dictionary<string, string>(), []);
    public IReadOnlyList<MusicVideoDetails> ArtistVideos { get; set; } = [];

    public Task<IReadOnlyList<ArtistSearchResult>> SearchArtistsAsync(string query, CancellationToken ct) =>
        Task.FromResult(SearchResults);
    public Task<ArtistDetails> GetArtistAsync(string providerId, CancellationToken ct) =>
        Task.FromResult(ArtistDetails);
    public Task<IReadOnlyList<MusicVideoDetails>> GetArtistVideosAsync(string providerId, CancellationToken ct) =>
        Task.FromResult(ArtistVideos);
    public Task<MusicVideoDetails> GetVideoAsync(string providerId, CancellationToken ct) =>
        throw new NotImplementedException();
}

internal sealed class StubIndexer : IIndexer
{
    public int Id => 1;
    public string Name => "stub";
    public DownloadProtocol Protocol => DownloadProtocol.Streaming;
    public bool SupportsRss => false;
    public bool SupportsSearch => true;
    public Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria c, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
    public Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
    public Task<IndexerTestResult> TestAsync(CancellationToken ct) =>
        Task.FromResult(new IndexerTestResult(true, null));
}

internal sealed class StubDownloadClient : IDownloadClient
{
    public int Id => 1;
    public string Name => "stub-dc";
    public DownloadProtocol Protocol => DownloadProtocol.Streaming;
    public IReadOnlyList<DownloadClientItem> Items { get; set; } = [];

    public Task<DownloadClientItemId> DownloadAsync(RemoteRelease r, CancellationToken ct) =>
        Task.FromResult(new DownloadClientItemId("x"));
    public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct) =>
        Task.FromResult(Items);
    public Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct) => Task.CompletedTask;
    public Task<DownloadClientTestResult> TestAsync(CancellationToken ct) =>
        Task.FromResult(new DownloadClientTestResult(true, null));
}

internal sealed class StubCommandQueue : ICommandQueue
{
    public List<ICommand> Enqueued { get; } = [];
    public ValueTask EnqueueAsync(ICommand command, CancellationToken ct)
    {
        Enqueued.Add(command);
        return ValueTask.CompletedTask;
    }
    public async IAsyncEnumerable<ICommand> DequeueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }
}
