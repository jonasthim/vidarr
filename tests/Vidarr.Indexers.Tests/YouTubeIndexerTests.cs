using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Indexers;
using Vidarr.Tests.Common;

namespace Vidarr.Indexers.Tests;

public class YouTubeIndexerTests
{
    private const string SampleEntry1 = """{"id":"abc123","title":"Daft Punk - Around the World","webpage_url":"https://www.youtube.com/watch?v=abc123","uploader":"DaftPunkVEVO","channel_id":"UCdaft","upload_date":"19970917","height":1080,"filesize_approx":52428800}""";
    private const string SampleEntry2 = """{"id":"def456","title":"Around the World (Live)","webpage_url":"https://www.youtube.com/watch?v=def456","uploader":"random_user","upload_date":"20100101","height":720}""";

    private static YouTubeIndexer Build(FakeProcessRunner runner, YouTubeIndexerSettings? settings = null) =>
        new(1, "YouTube",
            settings ?? YouTubeIndexerSettings.Default,
            runner,
            new FakeHttpClient(),
            new YouTubeQualityMapper());

    [Fact]
    public async Task Fetch_parses_yt_dlp_dump_json_lines()
    {
        var stdout = string.Join('\n', SampleEntry1, SampleEntry2);
        var runner = new FakeProcessRunner().WhenInvocation(
            inv => inv.Executable == "yt-dlp" && inv.Arguments.Any(a => a.StartsWith("ytsearch", StringComparison.Ordinal)),
            new ProcessResult(0, stdout, "", TimeSpan.FromMilliseconds(100)));

        var releases = await Build(runner).FetchAsync(new IndexerSearchCriteria("any", "Daft Punk", "Around the World", 1997, []), default);

        releases.Should().HaveCount(2);
        releases[0].Title.Should().Be("Daft Punk - Around the World");
        releases[0].SourceUrl.AbsoluteUri.Should().Be("https://www.youtube.com/watch?v=abc123");
        releases[0].PublishedAt.Should().Be(new DateTimeOffset(1997, 9, 17, 0, 0, 0, TimeSpan.Zero));
        releases[0].SizeBytes.Should().Be(52428800);
        releases[0].ExtraMetadata.Should().ContainKey("youtubeId").WhoseValue.Should().Be("abc123");
        releases[0].ExtraMetadata.Should().ContainKey("channelId").WhoseValue.Should().Be("UCdaft");
        releases[0].ExtraMetadata.Should().ContainKey("height").WhoseValue.Should().Be("1080");
        releases[1].Title.Should().Be("Around the World (Live)");
    }

    [Fact]
    public async Task Fetch_uses_artist_title_query_when_provided()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, "", "", TimeSpan.Zero));
        await Build(runner).FetchAsync(new IndexerSearchCriteria("ignored", "Daft Punk", "One More Time", 2000, []), default);
        var args = runner.Invocations.Single().Arguments;
        args.Should().Contain(s => s.Contains("Daft Punk - One More Time official music video", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fetch_falls_back_to_generic_query_when_artist_title_missing()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, "", "", TimeSpan.Zero));
        await Build(runner).FetchAsync(new IndexerSearchCriteria("", null, null, null, []), default);
        var args = runner.Invocations.Single().Arguments;
        args.Should().Contain(s => s.Contains("music video", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fetch_returns_empty_on_nonzero_exit_code()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(1, "", "boom", TimeSpan.Zero));
        var releases = await Build(runner).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Fetch_skips_invalid_json_lines()
    {
        var stdout = $"not json\n{SampleEntry1}\n{{ malformed";
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, stdout, "", TimeSpan.Zero));
        var releases = await Build(runner).FetchAsync(new IndexerSearchCriteria("q", "A", "B", 2000, []), default);
        releases.Should().HaveCount(1);
    }

    [Fact]
    public async Task RssSync_returns_empty_when_no_channels_configured()
    {
        var runner = new FakeProcessRunner();
        var releases = await Build(runner).RssSyncAsync(default);
        releases.Should().BeEmpty();
        runner.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task RssSync_uses_http_atom_feed_per_channel()
    {
        // Phase 6: channel RSS now hits the official feed URL directly instead of
        // shelling out to yt-dlp. The runner should NOT be touched at all.
        var runner = new FakeProcessRunner();
        var http = new FakeHttpClient();
        http.WhenRequest(r => r.Uri.Query.Contains("UCabc", StringComparison.Ordinal),
            HttpClientResponseFactory.Xml(SampleAtomFeed("UCabc", "Channel A")));
        http.WhenRequest(r => r.Uri.Query.Contains("UCdef", StringComparison.Ordinal),
            HttpClientResponseFactory.Xml(SampleAtomFeed("UCdef", "Channel B")));

        var settings = YouTubeIndexerSettings.Default with { ChannelIds = ["UCabc", "UCdef"] };
        var indexer = new YouTubeIndexer(1, "YouTube", settings, runner, http, new YouTubeQualityMapper());
        var releases = await indexer.RssSyncAsync(default);

        runner.Invocations.Should().BeEmpty();
        http.Requests.Should().HaveCount(2);
        http.Requests.All(r => r.Uri.AbsolutePath.EndsWith("/feeds/videos.xml", StringComparison.Ordinal)).Should().BeTrue();
        releases.Should().HaveCount(2);
        releases.Should().Contain(r => r.ExtraMetadata.GetValueOrDefault("channelId") == "UCabc");
        releases.Should().Contain(r => r.ExtraMetadata.GetValueOrDefault("channelId") == "UCdef");
    }

    private static string SampleAtomFeed(string channelId, string channelTitle) => $"""
    <?xml version="1.0" encoding="UTF-8"?>
    <feed xmlns:yt="http://www.youtube.com/xml/schemas/2015"
          xmlns:media="http://search.yahoo.com/mrss/"
          xmlns="http://www.w3.org/2005/Atom">
      <yt:channelId>{channelId}</yt:channelId>
      <title>{channelTitle}</title>
      <entry>
        <yt:videoId>video-from-{channelId}</yt:videoId>
        <title>Daft Punk - Around the World ({channelTitle})</title>
        <author><name>{channelTitle}</name></author>
        <published>2025-10-17T12:00:00+00:00</published>
        <media:group>
          <media:content url="https://www.youtube.com/v/x" height="1080"/>
        </media:group>
      </entry>
    </feed>
    """;

    [Fact]
    public async Task Test_returns_success_when_yt_dlp_reports_version()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, "2026.05.01", "", TimeSpan.Zero));
        var result = await Build(runner).TestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("2026.05.01");
    }

    [Fact]
    public async Task Test_returns_failure_when_yt_dlp_missing()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(127, "", "command not found", TimeSpan.Zero));
        var result = await Build(runner).TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("command not found");
    }

    [Fact]
    public async Task Entry_without_upload_date_yields_null_published()
    {
        const string entry = """{"id":"x","title":"t"}""";
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, entry, "", TimeSpan.Zero));
        var releases = await Build(runner).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Single().PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Entry_without_id_is_skipped()
    {
        const string entry = """{"title":"missing id"}""";
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, entry, "", TimeSpan.Zero));
        var releases = await Build(runner).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Should().BeEmpty();
    }

    [Fact]
    public void Protocol_is_streaming_and_capabilities_advertised()
    {
        var indexer = Build(new FakeProcessRunner());
        indexer.Protocol.Should().Be(DownloadProtocol.Streaming);
        indexer.SupportsRss.Should().BeTrue();
        indexer.SupportsSearch.Should().BeTrue();
        indexer.Id.Should().Be(1);
        indexer.Name.Should().Be("YouTube");
    }

    [Fact]
    public async Task Entry_without_webpage_url_falls_back_to_watch_url()
    {
        const string entry = """{"id":"abc","title":"t"}""";
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, entry, "", TimeSpan.Zero));
        var releases = await Build(runner).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Single().SourceUrl.AbsoluteUri.Should().Be("https://www.youtube.com/watch?v=abc");
    }

    [Fact]
    public async Task Malformed_upload_date_falls_back_to_null_published()
    {
        const string entry = """{"id":"x","title":"t","upload_date":"not-a-date"}""";
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, entry, "", TimeSpan.Zero));
        var releases = await Build(runner).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Single().PublishedAt.Should().BeNull();
    }
}
