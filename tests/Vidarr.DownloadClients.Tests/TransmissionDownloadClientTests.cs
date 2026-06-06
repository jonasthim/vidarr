using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Tests.Common;

namespace Vidarr.DownloadClients.Tests;

public class TransmissionDownloadClientTests
{
    private static readonly Uri SampleBase = new("http://localhost:9091/");

    private static TransmissionDownloadClient Build(FakeHttpClient http, string? username = null) =>
        new(1, "Transmission",
            new TransmissionSettings(SampleBase, username, username is null ? null : "pass"),
            http);

    private static RemoteRelease SampleRelease() => new(
        Info: new ReleaseInfo("Sample.Torrent", new Uri("https://x.example/t.torrent"),
            "magnet:?xt=urn:btih:ABC", 1000, DateTimeOffset.UtcNow, null, 5, 1,
            DownloadProtocol.Torrent, "Indexer", "6030", new Dictionary<string, string>()),
        Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997, Quality.Bluray1080p, null, []),
        Score: 0,
        RejectionReasons: [],
        MatchedMusicVideoIds: []);

    [Fact]
    public async Task Download_performs_409_session_id_dance()
    {
        var http = new FakeHttpClient();
        var firstCall = true;
        http.WhenRequest(_ => firstCall, r =>
        {
            firstCall = false;
            return Task.FromResult(new HttpClientResponse(
                409,
                new Dictionary<string, string> { ["X-Transmission-Session-Id"] = "session-abc" },
                "Conflict"));
        });
        http.WhenRequest(_ => true, r =>
        {
            r.Headers!.Should().ContainKey("X-Transmission-Session-Id")
                .WhoseValue.Should().Be("session-abc");
            return Task.FromResult(new HttpClientResponse(200, new Dictionary<string, string>(),
                """{"result":"success","arguments":{"torrent-added":{"id":7,"hashString":"hashOk","name":"Sample"}}}"""));
        });

        var id = await Build(http).DownloadAsync(SampleRelease(), default);
        id.Value.Should().Be("hashOk");
        http.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Download_uses_basic_auth_when_username_set()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200,
            new Dictionary<string, string>(),
            """{"result":"success","arguments":{"torrent-added":{"id":1,"hashString":"h","name":"n"}}}"""));

        await Build(http, "user1").DownloadAsync(SampleRelease(), default);
        http.Requests[0].Headers!.Should().ContainKey("Authorization");
        http.Requests[0].Headers!["Authorization"].Should().StartWith("Basic ");
    }

    [Fact]
    public async Task Download_uses_duplicate_when_torrent_already_present()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200,
            new Dictionary<string, string>(),
            """{"result":"success","arguments":{"torrent-duplicate":{"id":11,"hashString":"dup","name":"dup"}}}"""));

        var id = await Build(http).DownloadAsync(SampleRelease(), default);
        id.Value.Should().Be("dup");
    }

    [Fact]
    public async Task Download_throws_when_result_is_not_success()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200,
            new Dictionary<string, string>(), """{"result":"invalid argument"}"""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*invalid argument*");
    }

    [Fact]
    public async Task Download_throws_when_http_status_non_2xx()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*500*");
    }

    [Fact]
    public async Task GetItems_parses_torrent_list()
    {
        const string body = """
        {
            "result":"success",
            "arguments":{
                "torrents":[
                    {"id":1,"hashString":"h1","name":"A","totalSize":1000,"leftUntilDone":500,"status":4,"downloadDir":"/downloads","eta":120,"errorString":""},
                    {"id":2,"hashString":"h2","name":"B","totalSize":2000,"leftUntilDone":0,"status":6,"downloadDir":"/downloads","eta":-1,"errorString":""}
                ]
            }
        }
        """;
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(), body));
        var items = await Build(http).GetItemsAsync(default);
        items.Should().HaveCount(2);
        items[0].Status.Should().Be(DownloadItemStatus.Downloading);
        items[0].Eta.Should().Be(TimeSpan.FromSeconds(120));
        items[1].Status.Should().Be(DownloadItemStatus.CompletedReadyToImport);
        items[1].OutputPath.Should().Be("/downloads");
        items[1].Eta.Should().BeNull(); // eta=-1 not positive
    }

    [Fact]
    public async Task GetItems_returns_empty_when_torrents_missing()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"success","arguments":{}}"""));
        (await Build(http).GetItemsAsync(default)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 100L, DownloadItemStatus.Queued)]
    [InlineData(1, 100L, DownloadItemStatus.Downloading)]
    [InlineData(2, 100L, DownloadItemStatus.Downloading)]
    [InlineData(3, 100L, DownloadItemStatus.Downloading)]
    [InlineData(4, 100L, DownloadItemStatus.Downloading)]
    [InlineData(5, 0L, DownloadItemStatus.CompletedReadyToImport)]
    [InlineData(5, 50L, DownloadItemStatus.Downloading)]
    [InlineData(6, 0L, DownloadItemStatus.CompletedReadyToImport)]
    [InlineData(99, 0L, DownloadItemStatus.Queued)]
    public void Status_mapping_truth_table(int s, long left, DownloadItemStatus expected) =>
        TransmissionDownloadClient.MapStatus(s, left).Should().Be(expected);

    [Fact]
    public async Task Remove_posts_torrent_remove_with_delete_flag()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"success","arguments":{}}"""));
        await Build(http).RemoveAsync(new DownloadClientItemId("abc"), deleteData: true, default);

        var bodyText = ((HttpClientContent.Json)http.Requests.Single().Content!).Body;
        using var doc = JsonDocument.Parse(bodyText);
        doc.RootElement.GetProperty("method").GetString().Should().Be("torrent-remove");
        var args = doc.RootElement.GetProperty("arguments");
        args.GetProperty("delete-local-data").GetBoolean().Should().BeTrue();
        args.GetProperty("ids")[0].GetString().Should().Be("abc");
    }

    [Fact]
    public async Task Test_succeeds_on_session_get_success()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"success","arguments":{}}"""));
        (await Build(http).TestAsync(default)).Success.Should().BeTrue();
    }

    [Fact]
    public async Task Test_returns_failure_on_non_success_result()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"nope"}"""));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeFalse();
        r.Message.Should().Be("nope");
    }

    [Fact]
    public async Task Test_returns_failure_on_http_error()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeFalse();
        r.Message.Should().Contain("500");
    }
}
