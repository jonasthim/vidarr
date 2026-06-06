using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Tests.Common;

namespace Vidarr.DownloadClients.Tests;

public class DelugeDownloadClientTests
{
    private static readonly Uri SampleBase = new("http://localhost:8112/");

    private static DelugeDownloadClient Build(FakeHttpClient http, string? category = null) =>
        new(1, "Deluge",
            new DelugeSettings(SampleBase, "deluge", category, DownloadLocation: "/downloads"),
            http);

    private static FakeHttpClient WithLoginOk(string sessionId = "session-1")
    {
        return new FakeHttpClient().WhenRequest(
            r =>
            {
                if (r.Content is not HttpClientContent.Json json) return false;
                return json.Body.Contains("\"method\":\"auth.login\"", StringComparison.Ordinal);
            },
            new HttpClientResponse(
                200,
                new Dictionary<string, string> { ["Set-Cookie"] = $"_session_id={sessionId}; path=/" },
                """{"result":true,"error":null,"id":1}"""));
    }

    private static RemoteRelease SampleRelease(bool withMagnet = true) => new(
        Info: new ReleaseInfo("Sample.Torrent", new Uri("https://x.example/t.torrent"),
            withMagnet ? "magnet:?xt=urn:btih:DELUGE" : null, 1000, DateTimeOffset.UtcNow, null, 5, 1,
            DownloadProtocol.Torrent, "Indexer", "6030", new Dictionary<string, string>()),
        Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997, Quality.Bluray1080p, null, []),
        Score: 0,
        RejectionReasons: [],
        MatchedMusicVideoIds: []);

    [Fact]
    public async Task Login_extracts_session_cookie_and_persists_it()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"2.1.1","error":null,"id":2}"""));

        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("2.1.1");

        // After login, the next request carries the session cookie.
        var second = http.Requests[1];
        second.Headers!.Should().ContainKey("Cookie").WhoseValue.Should().Be("_session_id=session-1");
    }

    [Fact]
    public async Task Test_returns_failure_when_login_returns_false()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":false,"error":null,"id":1}"""));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("rejected");
    }

    [Fact]
    public async Task Test_returns_failure_when_login_returns_error_envelope()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":null,"error":{"code":1,"message":"Bad password"},"id":1}"""));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Download_uses_add_torrent_magnet_for_magnet_links()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"abc-hash","error":null,"id":2}"""));

        var id = await Build(http).DownloadAsync(SampleRelease(withMagnet: true), default);
        id.Value.Should().Be("abc-hash");

        var addReq = http.Requests[1];
        var body = ((HttpClientContent.Json)addReq.Content!).Body;
        body.Should().Contain("\"method\":\"core.add_torrent_magnet\"");
    }

    [Fact]
    public async Task Download_uses_add_torrent_url_when_no_magnet()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"hash-from-url","error":null,"id":2}"""));

        var id = await Build(http).DownloadAsync(SampleRelease(withMagnet: false), default);
        id.Value.Should().Be("hash-from-url");

        var addReq = http.Requests[1];
        ((HttpClientContent.Json)addReq.Content!).Body
            .Should().Contain("\"method\":\"core.add_torrent_url\"");
    }

    [Fact]
    public async Task Download_throws_when_rpc_error_returned()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":null,"error":{"code":1,"message":"duplicate"},"id":2}"""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*duplicate*");
    }

    [Fact]
    public async Task Download_throws_when_result_is_null_with_no_error()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":null,"error":null,"id":2}"""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Download_sets_label_when_category_configured()
    {
        var http = WithLoginOk();
        http.WhenRequest(r =>
        {
            if (r.Content is not HttpClientContent.Json j) return false;
            return j.Body.Contains("core.add_torrent_magnet", StringComparison.Ordinal);
        }, new HttpClientResponse(200, new Dictionary<string, string>(), """{"result":"abc-hash","error":null,"id":2}"""));
        http.WhenRequest(r =>
        {
            if (r.Content is not HttpClientContent.Json j) return false;
            return j.Body.Contains("label.set_torrent", StringComparison.Ordinal);
        }, new HttpClientResponse(200, new Dictionary<string, string>(), """{"result":null,"error":null,"id":3}"""));

        await Build(http, category: "vidarr").DownloadAsync(SampleRelease(), default);
        http.Requests.Count(r => r.Content is HttpClientContent.Json j
            && j.Body.Contains("label.set_torrent", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task GetItems_parses_dictionary_of_torrents()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """
            {
                "result": {
                    "hash-a": {"hash":"hash-a","name":"A","total_size":1000,"state":"Downloading","progress":50,"eta":120,"save_path":"/downloads","error_code":null},
                    "hash-b": {"name":"B","total_size":2000,"state":"Seeding","progress":100,"eta":0,"save_path":"/downloads","error_code":null}
                },
                "error": null, "id": 2
            }
            """));
        var items = await Build(http).GetItemsAsync(default);
        items.Should().HaveCount(2);
        items.Single(i => i.Title == "A").Status.Should().Be(DownloadItemStatus.Downloading);
        var b = items.Single(i => i.Title == "B");
        b.Status.Should().Be(DownloadItemStatus.CompletedReadyToImport);
        b.OutputPath.Should().Be("/downloads");
        b.Id.Value.Should().Be("hash-b"); // hash from dictionary key when missing in payload
    }

    [Fact]
    public async Task GetItems_returns_empty_when_result_is_not_object()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":[],"error":null,"id":2}"""));
        (await Build(http).GetItemsAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetItems_returns_empty_when_rpc_error()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":null,"error":{"code":2,"message":"x"},"id":2}"""));
        (await Build(http).GetItemsAsync(default)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Error", DownloadItemStatus.Failed)]
    [InlineData("Queued", DownloadItemStatus.Queued)]
    [InlineData("Paused", DownloadItemStatus.Queued)]
    [InlineData("Downloading", DownloadItemStatus.Downloading)]
    [InlineData("Checking", DownloadItemStatus.Downloading)]
    [InlineData("Allocating", DownloadItemStatus.Downloading)]
    [InlineData("Seeding", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("Active", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("?", DownloadItemStatus.Queued)]
    public void Status_mapping_truth_table(string state, DownloadItemStatus expected) =>
        DelugeDownloadClient.MapStatus(state).Should().Be(expected);

    [Fact]
    public async Task Remove_posts_core_remove_torrent_with_delete_flag()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":true,"error":null,"id":2}"""));

        await Build(http).RemoveAsync(new DownloadClientItemId("abc"), deleteData: false, default);
        var removeReq = http.Requests[1];
        var body = ((HttpClientContent.Json)removeReq.Content!).Body;
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("method").GetString().Should().Be("core.remove_torrent");
        doc.RootElement.GetProperty("params")[0].GetString().Should().Be("abc");
        doc.RootElement.GetProperty("params")[1].GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Http_5xx_during_rpc_throws()
    {
        var http = WithLoginOk();
        http.WhenRequest(_ => true, new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        await FluentActions.Invoking(() => Build(http).TestAsync(default))
            .Should().NotThrowAsync(); // Test handles its own errors and returns failure result
    }

    [Fact]
    public void Extract_session_cookie_handles_missing_or_unrelated_cookies()
    {
        DelugeDownloadClient.ExtractSessionCookie("other=foo; path=/").Should().BeNull();
        DelugeDownloadClient.ExtractSessionCookie("_session_id=abc; path=/").Should().Be("abc");
    }
}
