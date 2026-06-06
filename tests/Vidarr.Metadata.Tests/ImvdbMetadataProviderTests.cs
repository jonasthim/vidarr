using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.Metadata;
using Vidarr.Tests.Common;

namespace Vidarr.Metadata.Tests;

public class ImvdbMetadataProviderTests
{
    private static ImvdbMetadataProvider Build(FakeHttpClient http, string? apiKey = null) =>
        new(http, new ImvdbOptions(apiKey));

    [Fact]
    public async Task SearchArtists_parses_results()
    {
        const string body = """
        { "results": [
            { "id": 5489, "name": "Daft Punk", "country": "France", "url": "https://imvdb.com/n/daft-punk" }
        ] }
        """;
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath.EndsWith("/search/entities", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(body));

        var results = await Build(http).SearchArtistsAsync("Daft Punk", default);

        results.Should().HaveCount(1);
        results[0].ProviderId.Should().Be("5489");
        results[0].Name.Should().Be("Daft Punk");
        results[0].Country.Should().Be("France");
    }

    [Fact]
    public async Task SearchArtists_returns_empty_on_non_200()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        var results = await Build(http).SearchArtistsAsync("x", default);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchArtists_empty_results_array_yields_empty()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Json("""{ "results": [] }"""));
        (await Build(http).SearchArtistsAsync("x", default)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetArtist_parses_canonical_fields_and_extracts_youtube_channel_ids()
    {
        const string body = """
        {
            "id": 5489,
            "name": "Daft Punk",
            "country": "France",
            "image": "https://imvdb.com/i/daft.png",
            "url_slug": "daft-punk",
            "social_links": [
                { "type": "youtube", "url": "https://www.youtube.com/channel/UCDaftPunkOfficial/videos" },
                { "type": "twitter", "url": "https://twitter.com/daftpunk" }
            ]
        }
        """;
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath.Contains("/artist/5489", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(body));

        var details = await Build(http).GetArtistAsync("5489", default);

        details.ProviderId.Should().Be("5489");
        details.Name.Should().Be("Daft Punk");
        details.Country.Should().Be("France");
        details.Images.Should().HaveCount(1);
        details.ExternalIds.Should().ContainKey("imvdb").WhoseValue.Should().Be("5489");
        details.YouTubeChannelIds.Should().Equal("UCDaftPunkOfficial");
    }

    [Fact]
    public async Task GetArtist_throws_on_non_200()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(404, new Dictionary<string, string>(), ""));
        var sut = Build(http);
        await FluentActions.Invoking(() => sut.GetArtistAsync("123", default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetArtistVideos_maps_each_video()
    {
        const string body = """
        { "videos": [
            { "id": 1, "song_title": "Around the World", "year": 1997, "release_date": "1997-03-17",
              "featured": null, "directors": [{ "name": "Michel Gondry" }],
              "image": { "s": "https://imvdb.com/i/1.png" } },
            { "id": 2, "song_title": "One More Time", "year": 2000, "release_date": "2000-11-13",
              "featured": "official" }
        ] }
        """;
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath.EndsWith("/videos", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(body));

        var videos = await Build(http).GetArtistVideosAsync("5489", default);

        videos.Should().HaveCount(2);
        videos[0].Title.Should().Be("Around the World");
        videos[0].Year.Should().Be(1997);
        videos[0].ReleaseDate.Should().Be(new DateOnly(1997, 3, 17));
        videos[0].Director.Should().Be("Michel Gondry");
        videos[0].ThumbnailUrl!.AbsoluteUri.Should().Be("https://imvdb.com/i/1.png");
        videos[1].Title.Should().Be("One More Time");
    }

    [Fact]
    public async Task GetArtistVideos_returns_empty_on_non_200()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        (await Build(http).GetArtistVideosAsync("x", default)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetVideo_parses_single_video()
    {
        const string body = """
        { "id": 99, "song_title": "Material Girl", "year": 1984, "release_date": "1984-11-30",
          "featured": "lyric", "artists": [{ "id": 12, "name": "Madonna" }] }
        """;
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath.Contains("/video/99", StringComparison.Ordinal),
            HttpClientResponseFactory.Json(body));

        var video = await Build(http).GetVideoAsync("99", default);

        video.Title.Should().Be("Material Girl");
        video.Type.Should().Be(MusicVideoType.Lyric);
        video.ArtistProviderId.Should().Be("12");
    }

    [Fact]
    public async Task GetVideo_throws_on_non_200()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(404, new Dictionary<string, string>(), ""));
        await FluentActions.Invoking(() => Build(http).GetVideoAsync("1", default))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Api_key_header_is_attached_when_configured()
    {
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Json("""{ "results": [] }"""));
        await Build(http, apiKey: "secret-key").SearchArtistsAsync("x", default);
        http.Requests.Should().ContainSingle()
            .Which.Headers!.Should().ContainKey("IMVDB-APP-KEY")
            .WhoseValue.Should().Be("secret-key");
    }

    [Fact]
    public void Id_property_returns_imvdb()
    {
        Build(new FakeHttpClient()).Id.Should().Be("imvdb");
    }

    [Fact]
    public void Empty_options_have_no_api_key()
    {
        ImvdbOptions.Empty.ApiKey.Should().BeNull();
    }

    [Fact]
    public async Task YouTube_links_without_channel_segment_are_skipped()
    {
        const string body = """
        {
            "id": 1, "name": "X",
            "social_links": [
                { "type": "youtube", "url": "https://www.youtube.com/user/legacyhandle" },
                { "type": "youtube", "url": "https://www.youtube.com/channel/UCabc/featured" },
                { "type": "youtube", "url": "https://www.youtube.com/channel/UCdef?si=x" },
                { "type": "youtube", "url": null }
            ]
        }
        """;
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Json(body));
        var details = await Build(http).GetArtistAsync("1", default);
        details.YouTubeChannelIds.Should().Equal("UCabc", "UCdef");
    }

    [Fact]
    public async Task Featured_field_maps_to_video_type()
    {
        const string body = """
        { "videos": [
            { "id": 1, "song_title": "A", "year": 2000, "release_date": "2000-01-01", "featured": "live" },
            { "id": 2, "song_title": "B", "year": 2000, "release_date": "2000-01-01", "featured": "acoustic" },
            { "id": 3, "song_title": "C", "year": 2000, "release_date": "2000-01-01", "featured": "unknown-tag" }
        ] }
        """;
        var http = new FakeHttpClient().SetDefault(HttpClientResponseFactory.Json(body));
        var videos = await Build(http).GetArtistVideosAsync("x", default);
        videos.Select(v => v.Type).Should().Equal(MusicVideoType.Live, MusicVideoType.Acoustic, MusicVideoType.Official);
    }
}
