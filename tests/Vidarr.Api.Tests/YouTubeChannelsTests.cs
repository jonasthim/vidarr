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
using Vidarr.Contracts.Models;

namespace Vidarr.Api.Tests;

public class YouTubeChannelsTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;

    public YouTubeChannelsTests()
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
                s.AddSingleton<Vidarr.Contracts.Domain.IMetadataProvider>(new EmptyMetadataProvider());
                s.AddSingleton<Vidarr.Contracts.Domain.IIndexer>(new NoopIndexer());
                s.AddSingleton<Vidarr.Contracts.Domain.IDownloadClient>(new NoopDownloadClient());
                s.AddSingleton<Vidarr.Scheduler.ICommandQueue>(new NoopCommandQueue());
                s.AddSingleton<Vidarr.Contracts.Abstractions.ISystemClock, Vidarr.Tests.Common.FakeClock>();
                s.AddScoped<IMusicVideoRepository, MusicVideoRepository>();
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

    private async Task<int> SeedArtistAsync(params string[] channelIds)
    {
        using var scope = _host.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IArtistRepository>();
        var a = await repo.AddAsync(new Artist
        {
            Name = "Daft Punk",
            SortName = "Daft Punk",
            YouTubeChannelIdsJson = System.Text.Json.JsonSerializer.Serialize(channelIds),
        }, default);
        return a.Id;
    }

    [Fact]
    public async Task Artist_dto_exposes_youtube_channel_ids()
    {
        var id = await SeedArtistAsync("UCdaft", "UCvevo");
        var resp = await _client.GetFromJsonAsync<ArtistDto>(new Uri($"http://localhost/api/v1/artist/{id}"));
        resp.Should().NotBeNull();
        resp!.YouTubeChannelIds.Should().Equal("UCdaft", "UCvevo");
    }

    [Fact]
    public async Task Put_youtube_channels_updates_artist()
    {
        var id = await SeedArtistAsync();
        var resp = await _client.PutAsJsonAsync(
            new Uri($"http://localhost/api/v1/artist/{id}/youtube-channels"),
            new YouTubeChannelsRequest(["UCabc", "UCdef"]));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<ArtistDto>();
        updated!.YouTubeChannelIds.Should().Equal("UCabc", "UCdef");
    }

    [Fact]
    public async Task Put_youtube_channels_trims_dedupes_and_drops_blanks()
    {
        var id = await SeedArtistAsync();
        var resp = await _client.PutAsJsonAsync(
            new Uri($"http://localhost/api/v1/artist/{id}/youtube-channels"),
            new YouTubeChannelsRequest(["  UCdup ", "UCdup", "", "  ", "UCnew "]));
        var updated = await resp.Content.ReadFromJsonAsync<ArtistDto>();
        updated!.YouTubeChannelIds.Should().Equal("UCdup", "UCnew");
    }

    [Fact]
    public async Task Put_youtube_channels_404s_when_artist_missing()
    {
        var resp = await _client.PutAsJsonAsync(
            new Uri("http://localhost/api/v1/artist/9999/youtube-channels"),
            new YouTubeChannelsRequest(["UCx"]));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class EmptyMetadataProvider : Vidarr.Contracts.Domain.IMetadataProvider
    {
        public string Id => "stub";
        public Task<IReadOnlyList<ArtistSearchResult>> SearchArtistsAsync(string q, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArtistSearchResult>>([]);
        public Task<ArtistDetails> GetArtistAsync(string id, CancellationToken ct) =>
            Task.FromResult(new ArtistDetails(id, "", null, null, [], [], null, null, null, [], new Dictionary<string, string>(), []));
        public Task<IReadOnlyList<MusicVideoDetails>> GetArtistVideosAsync(string id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MusicVideoDetails>>([]);
        public Task<MusicVideoDetails> GetVideoAsync(string id, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class NoopIndexer : Vidarr.Contracts.Domain.IIndexer
    {
        public int Id => 0;
        public string Name => "noop";
        public DownloadProtocol Protocol => DownloadProtocol.Streaming;
        public bool SupportsRss => false;
        public bool SupportsSearch => false;
        public Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria c, CancellationToken ct) => Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
        public Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
        public Task<Vidarr.Contracts.Domain.IndexerTestResult> TestAsync(CancellationToken ct) => Task.FromResult(new Vidarr.Contracts.Domain.IndexerTestResult(true, null));
    }

    private sealed class NoopDownloadClient : Vidarr.Contracts.Domain.IDownloadClient
    {
        public int Id => 0;
        public string Name => "noop";
        public DownloadProtocol Protocol => DownloadProtocol.Streaming;
        public Task<DownloadClientItemId> DownloadAsync(RemoteRelease r, CancellationToken ct) => Task.FromResult(new DownloadClientItemId("x"));
        public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DownloadClientItem>>([]);
        public Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct) => Task.CompletedTask;
        public Task<Vidarr.Contracts.Domain.DownloadClientTestResult> TestAsync(CancellationToken ct) => Task.FromResult(new Vidarr.Contracts.Domain.DownloadClientTestResult(true, null));
    }

    private sealed class NoopCommandQueue : Vidarr.Scheduler.ICommandQueue
    {
        public ValueTask EnqueueAsync(Vidarr.Scheduler.ICommand c, CancellationToken ct) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<Vidarr.Scheduler.ICommand> DequeueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
