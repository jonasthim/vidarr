using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Indexers;
using Vidarr.Tests.Common;

namespace Vidarr.Indexers.Tests;

public class YouTubeIndexerHybridTests
{
    private const string SampleSearchListResponse = """
    {
        "items": [
            {
                "id": { "videoId": "abc123" },
                "snippet": {
                    "title": "Daft Punk - Around the World",
                    "channelTitle": "DaftPunkVEVO",
                    "channelId": "UCdaft",
                    "publishedAt": "1997-09-17T00:00:00Z"
                }
            },
            {
                "id": { "videoId": "def456" },
                "snippet": {
                    "title": "Daft Punk - One More Time",
                    "channelTitle": "DaftPunkVEVO",
                    "channelId": "UCdaft",
                    "publishedAt": "2000-11-13T00:00:00Z"
                }
            }
        ]
    }
    """;

    private const string SampleVideosListResponse = """
    {
        "items": [
            { "id": "abc123", "contentDetails": { "duration": "PT5M3S", "definition": "hd" } },
            { "id": "def456", "contentDetails": { "duration": "PT5M21S", "definition": "sd" } }
        ]
    }
    """;

    private static YouTubeIndexer Build(
        FakeHttpClient http,
        FakeProcessRunner? runner = null,
        string? dataApiKey = "test-api-key") =>
        new(1, "YouTube",
            YouTubeIndexerSettings.Default with { DataApiKey = dataApiKey },
            runner ?? new FakeProcessRunner(),
            http,
            new YouTubeQualityMapper());

    [Fact]
    public async Task Data_api_search_returns_items_and_enriches_with_videos_list()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(SampleSearchListResponse));
        http.WhenRequest(r => r.Uri.AbsolutePath.EndsWith("/videos", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(SampleVideosListResponse));

        var sut = Build(http);
        var releases = await sut.FetchAsync(
            new IndexerSearchCriteria("daft punk around the world", "Daft Punk", "Around the World", 1997, []), default);

        releases.Should().HaveCount(2);
        releases.Should().Contain(r => r.ExtraMetadata.GetValueOrDefault("youtubeId") == "abc123");
        // hd → 720; sd → 480
        var hd = releases.Single(r => r.ExtraMetadata.GetValueOrDefault("youtubeId") == "abc123");
        hd.ExtraMetadata.GetValueOrDefault("height").Should().Be("720");
        hd.ExtraMetadata.GetValueOrDefault("quality").Should().Be("WEBDL-720p");
    }

    [Fact]
    public async Task Data_api_search_url_carries_apikey_query_and_part_param()
    {
        var http = new FakeHttpClient();
        http.SetDefault(HttpClientResponseFactory.Json(SampleSearchListResponse));

        await Build(http).FetchAsync(new IndexerSearchCriteria("q", "Daft Punk", "Around the World", null, []), default);

        var searchReq = http.Requests.First(r => r.Uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal));
        searchReq.Uri.Authority.Should().Be("www.googleapis.com");
        searchReq.Uri.Query.Should().Contain("part=snippet")
            .And.Contain("type=video")
            .And.Contain("key=test-api-key");
    }

    [Fact]
    public async Task Quota_exceeded_on_search_falls_back_to_yt_dlp()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal),
            new HttpClientResponse(403, new Dictionary<string, string>(),
                """{"error":{"errors":[{"reason":"quotaExceeded","message":"Daily limit"}],"code":403}}"""));

        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0,
            """{"id":"yt-fallback","title":"From YT-DLP","webpage_url":"https://www.youtube.com/watch?v=yt-fallback","upload_date":"20251017","height":1080}""",
            "", TimeSpan.Zero));

        var sut = Build(http, runner);
        var releases = await sut.FetchAsync(new IndexerSearchCriteria("q", "Daft Punk", "Around the World", null, []), default);

        sut.DataApiQuotaExceeded.Should().BeTrue();
        releases.Should().ContainSingle().Which.ExtraMetadata.GetValueOrDefault("youtubeId").Should().Be("yt-fallback");
    }

    [Fact]
    public async Task Once_quota_exceeded_subsequent_calls_skip_the_api()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal),
            new HttpClientResponse(403, new Dictionary<string, string>(),
                """{"error":{"errors":[{"reason":"quotaExceeded"}]}}"""));
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0,
            """{"id":"yt","title":"x","webpage_url":"https://www.youtube.com/watch?v=yt"}""",
            "", TimeSpan.Zero));

        var sut = Build(http, runner);
        await sut.FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default); // primes quota
        var beforeCount = http.Requests.Count;
        await sut.FetchAsync(new IndexerSearchCriteria("q2", null, null, null, []), default);
        http.Requests.Count.Should().Be(beforeCount); // second call did NOT hit the API
    }

    [Fact]
    public async Task Data_api_non_200_falls_back_to_yt_dlp_without_marking_quota()
    {
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal),
            new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0,
            """{"id":"yt","title":"From fallback","webpage_url":"https://www.youtube.com/watch?v=yt"}""",
            "", TimeSpan.Zero));

        var sut = Build(http, runner);
        var releases = await sut.FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        sut.DataApiQuotaExceeded.Should().BeFalse();
        releases.Should().ContainSingle();
    }

    [Fact]
    public async Task No_api_key_skips_data_api_entirely_and_uses_yt_dlp()
    {
        var http = new FakeHttpClient(); // any request returns 404 by default
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0,
            """{"id":"yt","title":"yt-only","webpage_url":"https://www.youtube.com/watch?v=yt"}""",
            "", TimeSpan.Zero));

        var sut = Build(http, runner, dataApiKey: null);
        var releases = await sut.FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        http.Requests.Should().BeEmpty();
        releases.Should().ContainSingle();
    }

    [Fact]
    public async Task Test_with_data_api_key_hits_search_endpoint()
    {
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal),
            new HttpClientResponse(200, new Dictionary<string, string>(), "{\"items\":[]}"));
        var sut = Build(http);
        var result = await sut.TestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Data API");
    }

    [Fact]
    public async Task Test_data_api_failure_returns_failure_result()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(403, new Dictionary<string, string>(), "{}"));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("403");
    }

    [Fact]
    public async Task Test_without_data_api_key_falls_back_to_yt_dlp_version()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, "2026.05.01", "", TimeSpan.Zero));
        var result = await Build(new FakeHttpClient(), runner, dataApiKey: null).TestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("yt-dlp");
    }

    [Theory]
    [InlineData(403, """{"error":{"errors":[{"reason":"quotaExceeded"}]}}""", true)]
    [InlineData(403, """{"error":{"errors":[{"reason":"dailyLimitExceeded"}]}}""", true)]
    [InlineData(403, "{}", false)]
    [InlineData(403, "", true)]
    [InlineData(500, """{"error":{"errors":[{"reason":"quotaExceeded"}]}}""", false)]
    [InlineData(200, "{}", false)]
    public void IsQuotaExceeded_truth_table(int status, string body, bool expected) =>
        YouTubeIndexer.IsQuotaExceeded(new HttpClientResponse(status, new Dictionary<string, string>(), body))
            .Should().Be(expected);
}
