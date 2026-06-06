using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Indexers;
using Vidarr.Tests.Common;

namespace Vidarr.Indexers.Tests;

public class TorznabIndexerTests
{
    private static readonly Uri SampleBase = new("https://tor.example.com/");

    private const string TorznabFeed = """
    <?xml version="1.0" encoding="UTF-8"?>
    <rss version="2.0"
         xmlns:newznab="http://www.newznab.com/DTD/2010/feeds/attributes/"
         xmlns:torznab="http://torznab.com/schemas/2015/feed">
      <channel>
        <title>Feed</title>
        <item>
          <title>Daft Punk - Around the World (1997) BluRay 1080p</title>
          <guid isPermaLink="true">https://tor.example.com/d/abc</guid>
          <link>https://tor.example.com/t/abc.torrent</link>
          <pubDate>Fri, 17 Oct 2025 12:00:00 GMT</pubDate>
          <size>2147483648</size>
          <newznab:attr name="category" value="6030"/>
          <torznab:attr name="seeders" value="123"/>
          <torznab:attr name="peers" value="200"/>
          <torznab:attr name="magneturl" value="magnet:?xt=urn:btih:ABCDEFGH"/>
        </item>
      </channel>
    </rss>
    """;

    private static TorznabIndexer Build(FakeHttpClient http) =>
        new(2, "Torznab",
            NewznabIndexerSettings.WithDefaultCategories(SampleBase, "torkey"),
            http);

    [Fact]
    public async Task Fetch_parses_torznab_attrs_seeders_peers_magneturl()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(TorznabFeed));
        var releases = await Build(http).FetchAsync(new IndexerSearchCriteria("q", "Daft Punk", "Around the World", 1997, []), default);

        releases.Should().ContainSingle();
        var r = releases[0];
        r.Protocol.Should().Be(DownloadProtocol.Torrent);
        r.Seeders.Should().Be(123);
        r.Leechers.Should().Be(77); // peers - seeders = 200 - 123
        r.Magnet.Should().Be("magnet:?xt=urn:btih:ABCDEFGH");
        r.SizeBytes.Should().Be(2_147_483_648);
        r.ExtraMetadata.Should().ContainKey("torznab.seeders").WhoseValue.Should().Be("123");
    }

    [Fact]
    public async Task Items_without_seeders_or_peers_have_null_seeders()
    {
        const string noSeeders = """
        <?xml version="1.0"?>
        <rss version="2.0" xmlns:torznab="http://torznab.com/schemas/2015/feed">
          <channel>
            <item>
              <title>X</title>
              <link>https://tor.example.com/t/x</link>
              <pubDate>Fri, 17 Oct 2025 12:00:00 GMT</pubDate>
            </item>
          </channel>
        </rss>
        """;
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Xml(noSeeders));
        var release = (await Build(http).FetchAsync(new IndexerSearchCriteria("q", null, null, null, []), default)).Single();
        release.Seeders.Should().BeNull();
        release.Leechers.Should().BeNull();
        release.Magnet.Should().BeNull();
    }
}
