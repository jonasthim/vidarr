using System.Text;
using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Tests.Common;

namespace Vidarr.DownloadClients.Tests;

public class NZBGetDownloadClientTests
{
    private static readonly Uri SampleBase = new("http://localhost:6789/");

    private static NZBGetDownloadClient Build(FakeHttpClient http, string? category = null) =>
        new(1, "NZBGet", new NZBGetSettings(SampleBase, "user", "pass", category), http);

    private static RemoteRelease SampleRelease() => new(
        Info: new ReleaseInfo("Sample.NZB", new Uri("https://nzb.example.com/x.nzb"),
            null, 100, DateTimeOffset.UtcNow, null, null, null,
            DownloadProtocol.Usenet, "Indexer", "6030", new Dictionary<string, string>()),
        Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997, Quality.Bluray1080p, null, []),
        Score: 0, RejectionReasons: [], MatchedMusicVideoIds: []);

    [Fact]
    public async Task Download_sends_append_and_returns_nzbid()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(
            200, new Dictionary<string, string>(), """{"result":17,"error":null,"id":1}"""));

        var id = await Build(http, category: "vidarr").DownloadAsync(SampleRelease(), default);
        id.Value.Should().Be("17");

        var req = http.Requests.Single();
        var body = ((HttpClientContent.Json)req.Content!).Body;
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("method").GetString().Should().Be("append");
        var p = doc.RootElement.GetProperty("params");
        p[0].GetString().Should().Be("Sample.NZB");
        p[1].GetString().Should().Be("https://nzb.example.com/x.nzb");
        p[2].GetString().Should().Be("vidarr");

        req.Headers!.Should().ContainKey("Authorization");
        var expectedCreds = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"));
        req.Headers!["Authorization"].Should().Be($"Basic {expectedCreds}");
    }

    [Fact]
    public async Task Download_throws_when_error_envelope()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(
            200, new Dictionary<string, string>(),
            """{"result":null,"error":{"code":1,"message":"bad call"},"id":1}"""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*bad call*");
    }

    [Fact]
    public async Task Download_throws_when_result_zero_or_negative()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(
            200, new Dictionary<string, string>(), """{"result":0,"error":null,"id":1}"""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Download_throws_when_http_non_2xx()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetItems_merges_listgroups_and_history()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r =>
        {
            if (r.Content is not HttpClientContent.Json j) return false;
            return j.Body.Contains("\"method\":\"listgroups\"", StringComparison.Ordinal);
        }, new HttpClientResponse(200, new Dictionary<string, string>(),
            """
            {
                "result":[
                    {"NZBID":1,"NZBName":"G1","FileSizeMB":100,"RemainingSizeMB":50,"Status":"DOWNLOADING","Category":"vidarr"},
                    {"NZBID":2,"NZBName":"G2","FileSizeMB":200,"RemainingSizeMB":200,"Status":"QUEUED","Category":""}
                ],
                "error":null,"id":1
            }
            """));
        http.WhenRequest(r =>
        {
            if (r.Content is not HttpClientContent.Json j) return false;
            return j.Body.Contains("\"method\":\"history\"", StringComparison.Ordinal);
        }, new HttpClientResponse(200, new Dictionary<string, string>(),
            """
            {
                "result":[
                    {"NZBID":11,"Name":"H1","FileSizeMB":500,"Status":"SUCCESS/UNPACK","DestDir":"/downloads/h1","Category":""},
                    {"NZBID":12,"Name":"H2","FileSizeMB":1,"Status":"FAILURE/UNPACK","DestDir":"","Category":""}
                ],
                "error":null,"id":2
            }
            """));

        var items = await Build(http).GetItemsAsync(default);
        items.Should().HaveCount(4);
        items.Single(i => i.Title == "G1").Status.Should().Be(DownloadItemStatus.Downloading);
        items.Single(i => i.Title == "G2").Status.Should().Be(DownloadItemStatus.Queued);
        var h1 = items.Single(i => i.Title == "H1");
        h1.Status.Should().Be(DownloadItemStatus.CompletedReadyToImport);
        h1.OutputPath.Should().Be("/downloads/h1");
        items.Single(i => i.Title == "H2").Status.Should().Be(DownloadItemStatus.Failed);
    }

    [Fact]
    public async Task GetItems_filters_by_category_when_configured()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """
            {
                "result":[
                    {"NZBID":1,"NZBName":"X","FileSizeMB":1,"RemainingSizeMB":1,"Status":"DOWNLOADING","Category":"vidarr"},
                    {"NZBID":2,"NZBName":"Y","FileSizeMB":1,"RemainingSizeMB":1,"Status":"DOWNLOADING","Category":"other"}
                ],
                "error":null,"id":1
            }
            """));
        var items = await Build(http, category: "vidarr").GetItemsAsync(default);
        items.Should().HaveCount(2); // 1 group X kept + 0 history (also category filter applied)
        items.Should().Contain(i => i.Title == "X");
        items.Should().NotContain(i => i.Title == "Y");
    }

    [Fact]
    public async Task GetItems_returns_empty_when_rpc_error()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":null,"error":{"code":1,"message":"nope"},"id":1}"""));
        (await Build(http).GetItemsAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_uses_groupdelete_when_not_deleting_data()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":true,"error":null,"id":1}"""));
        await Build(http).RemoveAsync(new DownloadClientItemId("42"), deleteData: false, default);

        var body = ((HttpClientContent.Json)http.Requests.Single().Content!).Body;
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("params")[0].GetString().Should().Be("GroupDelete");
        doc.RootElement.GetProperty("params")[3][0].GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task Remove_uses_groupfinaldelete_when_deleting_data()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":true,"error":null,"id":1}"""));
        await Build(http).RemoveAsync(new DownloadClientItemId("42"), deleteData: true, default);

        var body = ((HttpClientContent.Json)http.Requests.Single().Content!).Body;
        body.Should().Contain("GroupFinalDelete");
    }

    [Fact]
    public async Task Remove_with_non_numeric_id_is_noop()
    {
        var http = new FakeHttpClient();
        await Build(http).RemoveAsync(new DownloadClientItemId("abc"), deleteData: true, default);
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_succeeds_on_version_string()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":"21.0","error":null,"id":1}"""));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeTrue();
        r.Message.Should().Contain("21.0");
    }

    [Fact]
    public async Task Test_returns_failure_when_error_envelope()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(),
            """{"result":null,"error":{"code":1,"message":"auth required"},"id":1}"""));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeFalse();
        r.Message.Should().Contain("auth required");
    }

    [Fact]
    public async Task Test_returns_failure_when_http_error()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeFalse();
        r.Message.Should().Contain("500");
    }

    [Theory]
    [InlineData("DOWNLOADING", DownloadItemStatus.Downloading)]
    [InlineData("FETCHING", DownloadItemStatus.Downloading)]
    [InlineData("REPAIRING", DownloadItemStatus.Downloading)]
    [InlineData("UNPACKING", DownloadItemStatus.Downloading)]
    [InlineData("QUEUED", DownloadItemStatus.Queued)]
    [InlineData("PAUSED", DownloadItemStatus.Queued)]
    [InlineData("?", DownloadItemStatus.Queued)]
    [InlineData(null, DownloadItemStatus.Queued)]
    public void Group_status_truth_table(string? state, DownloadItemStatus expected) =>
        NZBGetDownloadClient.MapGroupStatus(state).Should().Be(expected);

    [Theory]
    [InlineData("SUCCESS/UNPACK", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("SUCCESS/ALL", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("FAILURE/PAR", DownloadItemStatus.Failed)]
    [InlineData("WARNING/PAR", DownloadItemStatus.Failed)]
    [InlineData("DELETED/MANUAL", DownloadItemStatus.Removed)]
    [InlineData("DOWNLOADING", DownloadItemStatus.Downloading)]
    [InlineData(null, DownloadItemStatus.Downloading)]
    public void History_status_truth_table(string? state, DownloadItemStatus expected) =>
        NZBGetDownloadClient.MapHistoryStatus(state).Should().Be(expected);
}
