using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Tests.Common;

namespace Vidarr.DownloadClients.Tests;

public class SABnzbdDownloadClientTests
{
    private static readonly Uri SampleBase = new("http://localhost:8080/");

    private static SABnzbdDownloadClient Build(FakeHttpClient http, string? category = null, int? priority = null) =>
        new(1, "SAB",
            new SABnzbdSettings(SampleBase, "secret-key", category, priority),
            http);

    private static RemoteRelease SampleRelease() => new(
        Info: new ReleaseInfo("Sample.NZB", new Uri("https://nzb.example.com/x.nzb"),
            null, 1000, DateTimeOffset.UtcNow, null, null, null,
            DownloadProtocol.Usenet, "Indexer", "6030", new Dictionary<string, string>()),
        Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997, Quality.Bluray1080p, null, []),
        Score: 0, RejectionReasons: [], MatchedMusicVideoIds: []);

    [Fact]
    public async Task Add_calls_addurl_with_nzbname_apikey_category_priority()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(
            200, new Dictionary<string, string>(), """{"status":true,"nzo_ids":["SABnzbd_nzo_abc"]}"""));

        var id = await Build(http, category: "vidarr", priority: 1).DownloadAsync(SampleRelease(), default);
        id.Value.Should().Be("SABnzbd_nzo_abc");

        var req = http.Requests.Single();
        req.Uri.AbsolutePath.Should().Be("/api");
        req.Uri.Query.Should().Contain("mode=addurl")
            .And.Contain("apikey=secret-key")
            .And.Contain("cat=vidarr")
            .And.Contain("priority=1")
            .And.Contain("output=json");
    }

    [Fact]
    public async Task Add_throws_when_status_false()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(
            200, new Dictionary<string, string>(), """{"status":false,"error":"bad apikey"}"""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*bad apikey*");
    }

    [Fact]
    public async Task Add_throws_when_no_nzo_id_returned()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(
            200, new Dictionary<string, string>(), """{"status":true,"nzo_ids":[]}"""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Add_throws_on_non_2xx()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        await FluentActions.Invoking(() => Build(http).DownloadAsync(SampleRelease(), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*500*");
    }

    [Fact]
    public async Task GetItems_merges_queue_and_history()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.Query.Contains("mode=queue", StringComparison.Ordinal),
            new HttpClientResponse(200, new Dictionary<string, string>(), """
            {
                "queue": {
                    "slots": [
                        { "nzo_id":"q1", "filename":"Q-One", "status":"Downloading",
                          "mb":"100.5", "mbleft":"50.25", "timeleft":"0:01:30" }
                    ]
                }
            }
            """));
        http.WhenRequest(r => r.Uri.Query.Contains("mode=history", StringComparison.Ordinal),
            new HttpClientResponse(200, new Dictionary<string, string>(), """
            {
                "history": {
                    "slots": [
                        { "nzo_id":"h1", "name":"H-Done", "status":"Completed",
                          "bytes":104857600, "storage":"/downloads/h1" },
                        { "nzo_id":"h2", "name":"H-Failed", "status":"Failed",
                          "bytes":0, "fail_message":"par2 failed" }
                    ]
                }
            }
            """));

        var items = await Build(http).GetItemsAsync(default);
        items.Should().HaveCount(3);

        var q = items.Single(i => i.Title == "Q-One");
        q.Status.Should().Be(DownloadItemStatus.Downloading);
        q.Eta.Should().Be(new TimeSpan(0, 1, 30));
        q.TotalBytes.Should().NotBeNull().And.BeInRange(104_800_000, 105_400_000);

        var h = items.Single(i => i.Title == "H-Done");
        h.Status.Should().Be(DownloadItemStatus.CompletedReadyToImport);
        h.OutputPath.Should().Be("/downloads/h1");

        var f = items.Single(i => i.Title == "H-Failed");
        f.Status.Should().Be(DownloadItemStatus.Failed);
        f.Message.Should().Be("par2 failed");
    }

    [Fact]
    public async Task GetItems_returns_empty_when_queue_and_history_unreachable()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        (await Build(http).GetItemsAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetItems_search_param_added_when_category_configured()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(_ => true,
            new HttpClientResponse(200, new Dictionary<string, string>(), """{"queue":{"slots":[]}, "history":{"slots":[]}}"""));
        await Build(http, category: "vidarr").GetItemsAsync(default);
        http.Requests.Should().Contain(r =>
            r.Uri.Query.Contains("mode=queue", StringComparison.Ordinal)
            && r.Uri.Query.Contains("search=vidarr", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Queued", DownloadItemStatus.Queued)]
    [InlineData("Paused", DownloadItemStatus.Queued)]
    [InlineData("Downloading", DownloadItemStatus.Downloading)]
    [InlineData("Verifying", DownloadItemStatus.Downloading)]
    [InlineData("Repairing", DownloadItemStatus.Downloading)]
    [InlineData("Extracting", DownloadItemStatus.Downloading)]
    [InlineData("Moving", DownloadItemStatus.Downloading)]
    [InlineData("Completed", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("Failed", DownloadItemStatus.Failed)]
    [InlineData("Unknown", DownloadItemStatus.Queued)]
    [InlineData(null, DownloadItemStatus.Queued)]
    public void Queue_status_truth_table(string? state, DownloadItemStatus expected) =>
        SABnzbdDownloadClient.MapQueueStatus(state).Should().Be(expected);

    [Theory]
    [InlineData("Completed", DownloadItemStatus.CompletedReadyToImport)]
    [InlineData("Failed", DownloadItemStatus.Failed)]
    [InlineData("Running", DownloadItemStatus.Downloading)]
    public void History_status_truth_table(string state, DownloadItemStatus expected) =>
        SABnzbdDownloadClient.MapHistoryStatus(state).Should().Be(expected);

    [Fact]
    public async Task Remove_calls_queue_delete_first()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.Query.Contains("mode=queue", StringComparison.Ordinal) && r.Uri.Query.Contains("name=delete", StringComparison.Ordinal),
            new HttpClientResponse(200, new Dictionary<string, string>(), "{}"));

        await Build(http).RemoveAsync(new DownloadClientItemId("abc"), deleteData: true, default);

        var req = http.Requests.Single();
        req.Uri.Query.Should().Contain("mode=queue")
            .And.Contain("name=delete")
            .And.Contain("value=abc")
            .And.Contain("del_files=1");
    }

    [Fact]
    public async Task Remove_falls_back_to_history_when_queue_delete_fails()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.Query.Contains("mode=queue", StringComparison.Ordinal),
            new HttpClientResponse(404, new Dictionary<string, string>(), ""));
        http.WhenRequest(r => r.Uri.Query.Contains("mode=history", StringComparison.Ordinal),
            new HttpClientResponse(200, new Dictionary<string, string>(), "{}"));

        await Build(http).RemoveAsync(new DownloadClientItemId("abc"), deleteData: false, default);

        http.Requests.Should().HaveCount(2);
        http.Requests[1].Uri.Query.Should().Contain("mode=history");
    }

    [Fact]
    public async Task Test_returns_version_on_success()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(
            200, new Dictionary<string, string>(), """{"version":"4.3.1"}"""));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("4.3.1");
    }

    [Fact]
    public async Task Test_returns_failure_for_non_2xx()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(403, new Dictionary<string, string>(), ""));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeFalse();
        r.Message.Should().Contain("403");
    }

    [Fact]
    public async Task Test_returns_failure_when_no_version_in_body()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(), "{}"));
        var r = await Build(http).TestAsync(default);
        r.Success.Should().BeFalse();
    }

    [Fact]
    public void Protocol_is_usenet()
    {
        var http = new FakeHttpClient();
        Build(http).Protocol.Should().Be(DownloadProtocol.Usenet);
    }
}
