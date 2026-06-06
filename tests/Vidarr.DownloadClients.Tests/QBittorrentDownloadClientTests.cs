using System.Text;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Tests.Common;

namespace Vidarr.DownloadClients.Tests;

public class QBittorrentDownloadClientTests
{
    private static readonly Uri SampleBase = new("http://localhost:8080/");

    private static QBittorrentDownloadClient Build(FakeHttpClient http, string? category = null) =>
        new(1, "qBit",
            new QBittorrentSettings(SampleBase, "admin", "adminadmin", category),
            http);

    private static FakeHttpClient WithLoginOk() =>
        new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath == "/api/v2/auth/login",
            new HttpClientResponse(200,
                new Dictionary<string, string> { ["Set-Cookie"] = "SID=cookie123; HttpOnly; path=/" },
                "Ok."));

    private static RemoteRelease SampleRelease() => new(
        Info: new ReleaseInfo("Sample.Torrent", new Uri("https://x.example/t.torrent"),
            "magnet:?xt=urn:btih:ABC", 1000, DateTimeOffset.UtcNow, null, 5, 1,
            DownloadProtocol.Torrent, "Indexer", "6030", new Dictionary<string, string>()),
        Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997, Quality.Bluray1080p, null, []),
        Score: 0,
        RejectionReasons: [],
        MatchedMusicVideoIds: []);

    [Fact]
    public async Task Login_sends_form_credentials_and_extracts_sid_cookie()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/app/version",
            new HttpClientResponse(200, new Dictionary<string, string>(), "v4.5.0"));

        var test = await Build(http).TestAsync(default);
        test.Success.Should().BeTrue();
        test.Message.Should().Contain("v4.5.0");

        // First request is login, second is version
        var login = http.Requests[0];
        login.Uri.AbsolutePath.Should().Be("/api/v2/auth/login");
        var bodyBytes = ((HttpClientContent.Bytes)login.Content!).Body;
        Encoding.UTF8.GetString(bodyBytes).Should().Contain("username=admin").And.Contain("password=adminadmin");
        login.Headers!.Should().NotContainKey("Cookie");

        var version = http.Requests[1];
        version.Headers!.Should().ContainKey("Cookie").WhoseValue.Should().Be("SID=cookie123");
    }

    [Fact]
    public async Task Test_returns_failure_when_login_returns_fails_string()
    {
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath == "/api/v2/auth/login",
            new HttpClientResponse(200, new Dictionary<string, string>(), "Fails."));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("rejected");
    }

    [Fact]
    public async Task Test_returns_failure_when_login_returns_non_2xx()
    {
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath == "/api/v2/auth/login",
            new HttpClientResponse(403, new Dictionary<string, string>(), ""));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("403");
    }

    [Fact]
    public async Task Test_returns_failure_when_no_cookie_returned()
    {
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath == "/api/v2/auth/login",
            new HttpClientResponse(200, new Dictionary<string, string>(), "Ok."));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("SID");
    }

    [Fact]
    public async Task Download_posts_magnet_url_with_optional_category()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/add",
            new HttpClientResponse(200, new Dictionary<string, string>(), "Ok."));

        var id = await Build(http, "vidarr").DownloadAsync(SampleRelease(), default);
        id.Value.Should().StartWith("magnet:");

        var addReq = http.Requests.Single(r => r.Uri.AbsolutePath == "/api/v2/torrents/add");
        var body = Encoding.UTF8.GetString(((HttpClientContent.Bytes)addReq.Content!).Body);
        body.Should().Contain("urls=").And.Contain("category=vidarr");
    }

    [Fact]
    public async Task Download_falls_back_to_source_url_when_no_magnet()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/add",
            new HttpClientResponse(200, new Dictionary<string, string>(), "Ok."));

        var release = SampleRelease() with { Info = SampleRelease().Info with { Magnet = null } };
        var id = await Build(http).DownloadAsync(release, default);
        id.Value.Should().Be("https://x.example/t.torrent");
    }

    [Fact]
    public async Task Download_throws_when_add_returns_non_2xx()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/add",
            new HttpClientResponse(500, new Dictionary<string, string>(), ""));

        var sut = Build(http);
        await FluentActions.Invoking(() => sut.DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetItems_parses_qBit_torrent_list()
    {
        const string body = """
        [
            {
                "hash":"abc123","name":"Daft Punk - Around the World",
                "size":1048576,"amount_left":524288,"state":"downloading",
                "save_path":"/downloads/abc","eta":120
            },
            {
                "hash":"def456","name":"Daft Punk - One More Time",
                "size":2097152,"amount_left":0,"state":"uploading",
                "save_path":"/downloads/def","eta":8640000
            }
        ]
        """;
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/info",
            new HttpClientResponse(200, new Dictionary<string, string>(), body));

        var items = await Build(http).GetItemsAsync(default);
        items.Should().HaveCount(2);
        items[0].Status.Should().Be(DownloadItemStatus.Downloading);
        items[0].Eta.Should().Be(TimeSpan.FromSeconds(120));
        items[0].OutputPath.Should().BeNull(); // not yet completed
        items[1].Status.Should().Be(DownloadItemStatus.CompletedReadyToImport);
        items[1].OutputPath.Should().Be("/downloads/def");
        items[1].Eta.Should().BeNull(); // sentinel
    }

    [Fact]
    public async Task GetItems_includes_category_in_query_string()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/info",
            new HttpClientResponse(200, new Dictionary<string, string>(), "[]"));
        await Build(http, "vidarr").GetItemsAsync(default);
        var req = http.Requests.Single(r => r.Uri.AbsolutePath == "/api/v2/torrents/info");
        req.Uri.Query.Should().Contain("category=vidarr");
    }

    [Fact]
    public async Task GetItems_returns_empty_on_non_200()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/info",
            new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        (await Build(http).GetItemsAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetItems_returns_empty_on_empty_body()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/info",
            new HttpClientResponse(200, new Dictionary<string, string>(), ""));
        (await Build(http).GetItemsAsync(default)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("error", DownloadItemStatus.Failed)]
    [InlineData("missingFiles", DownloadItemStatus.Failed)]
    [InlineData("pausedDL", DownloadItemStatus.Queued)]
    [InlineData("queuedDL", DownloadItemStatus.Queued)]
    [InlineData("stalledDL", DownloadItemStatus.Queued)]
    [InlineData("downloading", DownloadItemStatus.Downloading)]
    [InlineData("metaDL", DownloadItemStatus.Downloading)]
    [InlineData("checkingDL", DownloadItemStatus.Downloading)]
    [InlineData("moving", DownloadItemStatus.Downloading)]
    [InlineData("uploading", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("forcedUP", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("pausedUP", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("unknown-state", DownloadItemStatus.Queued)]
    public void Status_mapping_truth_table(string state, DownloadItemStatus expected) =>
        QBittorrentDownloadClient.MapStatus(state).Should().Be(expected);

    [Fact]
    public async Task Remove_posts_hashes_and_delete_flag()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/delete",
            new HttpClientResponse(200, new Dictionary<string, string>(), ""));

        await Build(http).RemoveAsync(new DownloadClientItemId("abc"), deleteData: true, default);

        var req = http.Requests.Single(r => r.Uri.AbsolutePath == "/api/v2/torrents/delete");
        var body = Encoding.UTF8.GetString(((HttpClientContent.Bytes)req.Content!).Body);
        body.Should().Contain("hashes=abc").And.Contain("deleteFiles=true");
    }

    [Fact]
    public async Task Re_authentication_is_not_repeated_after_first_success()
    {
        var http = WithLoginOk();
        http.WhenRequest(r => r.Uri.AbsolutePath == "/api/v2/torrents/info",
            new HttpClientResponse(200, new Dictionary<string, string>(), "[]"));

        var client = Build(http);
        await client.GetItemsAsync(default);
        await client.GetItemsAsync(default);
        await client.GetItemsAsync(default);

        http.Requests.Count(r => r.Uri.AbsolutePath == "/api/v2/auth/login").Should().Be(1);
    }

    [Fact]
    public void Extract_sid_cookie_returns_null_when_absent()
    {
        QBittorrentDownloadClient.ExtractSidCookie(new Dictionary<string, string>()).Should().BeNull();
        QBittorrentDownloadClient.ExtractSidCookie(new Dictionary<string, string> { ["Set-Cookie"] = "other=foo" }).Should().BeNull();
    }
}
