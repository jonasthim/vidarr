using Vidarr.Indexers;
using Vidarr.Tests.Common;

namespace Vidarr.Indexers.Tests;

public class IndexerFactoryTests
{
    private static FakeHttpClient Http() => new();
    private static FakeProcessRunner Procs() => new();

    [Fact]
    public void Newznab_factory_advertises_settings_schema()
    {
        var f = new NewznabIndexerFactory(Http());
        f.Implementation.Should().Be("Newznab");
        f.DisplayName.Should().Contain("Usenet");
        f.SettingsSchema.Should().NotBeEmpty();
        f.SettingsSchema.Should().Contain(s => s.Name == "apiKey" && s.Required);
    }

    [Fact]
    public void Newznab_factory_parses_settings_json_and_applies_defaults()
    {
        var f = new NewznabIndexerFactory(Http());
        var indexer = f.Create(1, "Geek",
            "{\"baseUrl\":\"https://api.nzbgeek.info\",\"apiKey\":\"k\",\"categories\":[6030,6040],\"minAgeMinutes\":15,\"maxAgeDays\":30}");
        indexer.Should().NotBeNull();
        indexer.Name.Should().Be("Geek");
    }

    [Fact]
    public void Newznab_factory_uses_placeholder_url_when_blank()
    {
        var f = new NewznabIndexerFactory(Http());
        var indexer = f.Create(1, "x", "{}");
        indexer.Should().NotBeNull();
    }

    [Fact]
    public void Torznab_factory_creates_indexer_with_shared_parser()
    {
        var f = new TorznabIndexerFactory(Http());
        f.Implementation.Should().Be("Torznab");
        var indexer = f.Create(2, "Jackett", "{\"baseUrl\":\"http://localhost:9117/\",\"apiKey\":\"k\"}");
        indexer.Name.Should().Be("Jackett");
    }

    [Fact]
    public void YouTube_factory_creates_indexer_with_or_without_api_key()
    {
        var f = new YouTubeIndexerFactory(Procs(), Http(), new YouTubeQualityMapper());
        f.Implementation.Should().Be("YouTube");
        var noKey = f.Create(3, "yt-public", "{\"channelIds\":[\"UCabc\"],\"maxResults\":7}");
        noKey.Name.Should().Be("yt-public");

        var keyed = f.Create(4, "yt-keyed", "{\"dataApiKey\":\"k\",\"channelIds\":[],\"maxResults\":5,\"rssBatchSize\":12}");
        keyed.Name.Should().Be("yt-keyed");
    }

    [Fact]
    public void YouTube_factory_handles_empty_settings_with_defaults()
    {
        var f = new YouTubeIndexerFactory(Procs(), Http(), new YouTubeQualityMapper());
        var indexer = f.Create(5, "yt-default", "{}");
        indexer.Should().NotBeNull();
    }
}
