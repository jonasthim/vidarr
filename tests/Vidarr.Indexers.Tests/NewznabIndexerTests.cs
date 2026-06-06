using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Indexers;
using Vidarr.Tests.Common;

namespace Vidarr.Indexers.Tests;

public class NewznabIndexerTests
{
    private static readonly Uri SampleBase = new("https://nzb.example.com/");

    private const string SampleFeed = """
    <?xml version="1.0" encoding="UTF-8"?>
    <rss version="2.0"
         xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/"
         xmlns:torznab="http://torznab.com/schemas/2015/feed">
      <channel>
        <title>Feed</title>
        <item>
          <title>Daft Punk - Around the World (1997) WEB-DL 1080p H.264-GROUP</title>
          <guid isPermaLink="true">https://nzb.example.com/details/abc</guid>
          <link>https://nzb.example.com/getnzb/abc.nzb&amp;i=1&amp;r=secret</link>
          <pubDate>Fri, 17 Oct 2025 12:00:00 GMT</pubDate>
          <category>6030</category>
          <size>104857600</size>
          <newznab:attr name="category" value="6030"/>
          <newznab:attr name="size" value="104857600"/>
        </item>
        <item>
          <title>Daft Punk - One More Time (2000) WEB-DL 720p</title>
          <guid isPermaLink="true">https://nzb.example.com/details/def</guid>
          <link>https://nzb.example.com/getnzb/def.nzb</link>
          <pubDate>Mon, 01 Jan 2024 00:00:00 GMT</pubDate>
          <category>6030</category>
        </item>
      </channel>
    </rss>
    """;

    private static NewznabIndexer Build(FakeHttpClient http, NewznabIndexerSettings? settings = null) =>
        new(1, "Newznab", settings ?? NewznabIndexerSettings.WithDefaultCategories(SampleBase, "apikey-1"), http);

    [Fact]
    public async Task Fetch_parses_two_items_with_metadata()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(SampleFeed));
        var releases = await Build(http).FetchAsync(
            new IndexerSearchCriteria("any", "Daft Punk", "Around the World", 1997, []), default);

        releases.Should().HaveCount(2);
        releases[0].Title.Should().Be("Daft Punk - Around the World (1997) WEB-DL 1080p H.264-GROUP");
        releases[0].Protocol.Should().Be(DownloadProtocol.Usenet);
        releases[0].SizeBytes.Should().Be(104_857_600);
        releases[0].PublishedAt.Should().NotBeNull();
        releases[0].ExtraMetadata.Should().ContainKey("guid");
        releases[0].IndexerCategory.Should().Be("6030");
    }

    [Fact]
    public async Task Fetch_url_includes_apikey_t_q_and_categories()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(SampleFeed));
        await Build(http).FetchAsync(new IndexerSearchCriteria("any", "Daft Punk", "Around the World", 1997, []), default);
        var req = http.Requests.Should().ContainSingle().Which;
        req.Method.Should().Be(HttpMethod.Get);
        req.Uri.Query.Should().Contain("t=search").And.Contain("apikey=apikey-1").And.Contain("cat=6030");
        req.Uri.Query.Should().Contain("Daft+Punk").And.Contain("Around+the+World");
    }

    [Fact]
    public async Task RssSync_omits_q_param_so_indexer_returns_recent_items()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(SampleFeed));
        await Build(http).RssSyncAsync(default);
        var req = http.Requests.Should().ContainSingle().Which;
        req.Uri.Query.Should().NotContain("q=").And.Contain("t=search");
    }

    [Fact]
    public async Task Fetch_returns_empty_on_non_200()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(503, new Dictionary<string, string>(), ""));
        var releases = await Build(http).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Fetch_returns_empty_on_malformed_xml()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml("<this is not xml"));
        var releases = await Build(http).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Items_without_title_or_invalid_link_are_skipped()
    {
        const string broken = """
        <?xml version="1.0"?>
        <rss version="2.0" xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/">
          <channel>
            <item><link>https://x.example/nzb</link></item>
            <item><title>Has title but no link</title></item>
            <item><title>Bad link</title><link>not-a-uri</link></item>
            <item>
              <title>Valid</title>
              <link>https://x.example/nzb</link>
              <pubDate>Sat, 01 Nov 2025 00:00:00 GMT</pubDate>
            </item>
          </channel>
        </rss>
        """;
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(broken));
        var releases = await Build(http).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Should().ContainSingle().Which.Title.Should().Be("Valid");
    }

    [Fact]
    public async Task Min_and_max_age_filter_results()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(SampleFeed));
        // First item published 2025-10-17 (relatively recent); second item from 2024-01-01.
        var settings = NewznabIndexerSettings.WithDefaultCategories(SampleBase, "k") with { MinAgeMinutes = 60 * 24 * 365 };
        var releases = await Build(http, settings).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        // The recent item is filtered out by MinAge; only the older 2024 one survives.
        releases.Should().ContainSingle().Which.Title.Should().Contain("One More Time");

        var maxAgeSettings = NewznabIndexerSettings.WithDefaultCategories(SampleBase, "k") with { MaxAgeDays = 1 };
        var http2 = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(SampleFeed));
        var releases2 = await Build(http2, maxAgeSettings).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases2.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_body_returns_empty()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(""));
        var releases = await Build(http).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default);
        releases.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_returns_success_for_200()
    {
        var http = new FakeHttpClient().WhenRequest(r => r.Uri.Query.Contains("t=caps"), HttpClientResponseFactory.Xml("<caps/>"));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Be("OK");
    }

    [Fact]
    public async Task Test_returns_failure_for_500()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), "fail"));
        var result = await Build(http).TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("500");
    }

    [Fact]
    public async Task Falls_back_to_generic_query_when_artist_and_title_missing()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(SampleFeed));
        await Build(http).FetchAsync(new IndexerSearchCriteria("just a query", null, null, null, []), default);
        http.Requests.Single().Uri.Query.Should().Contain("q=just+a+query");
    }

    [Fact]
    public async Task Newznab_protocol_is_usenet()
    {
        var http = new FakeHttpClient();
        Build(http).Protocol.Should().Be(DownloadProtocol.Usenet);
        Build(http).SupportsRss.Should().BeTrue();
        Build(http).SupportsSearch.Should().BeTrue();
        await Task.CompletedTask;
    }
}
