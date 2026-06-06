using Vidarr.Contracts.Models;
using Vidarr.Decision;

namespace Vidarr.Decision.Tests;

public class ReleaseParserTests
{
    private readonly IReleaseParser _sut = new ReleaseParser();

    [Theory]
    [InlineData("Daft Punk - Around the World", "Daft Punk", "Around the World", null)]
    [InlineData("Daft Punk - Around the World (1997)", "Daft Punk", "Around the World", 1997)]
    [InlineData("Madonna - Material Girl [1984]", "Madonna", "Material Girl", 1984)]
    [InlineData("Aphex Twin - Windowlicker (1999) [Official Music Video]", "Aphex Twin", "Windowlicker", 1999)]
    public void Parses_artist_title_year(string title, string expectedArtist, string expectedTitle, int? expectedYear)
    {
        var parsed = _sut.Parse(title);

        parsed.ArtistName.Should().Be(expectedArtist);
        parsed.Title.Should().Be(expectedTitle);
        parsed.Year.Should().Be(expectedYear);
    }

    [Theory]
    [InlineData("Daft Punk - Around the World (1997) WEB-DL 1080p H.264-GROUP", 4)]
    [InlineData("Daft Punk - Around the World 1997 WEBDL 720p", 3)]
    [InlineData("Madonna - Material Girl (1984) BluRay-1080p", 10)]
    [InlineData("Madonna - Material Girl (1984) BDRip 720p", 9)]
    [InlineData("Artist - Title 2020 HDTV 720p", 6)]
    [InlineData("Artist - Title 2020 HDTV-1080p", 7)]
    [InlineData("Artist - Title 2020 DVDRip", 8)]
    [InlineData("Artist - Title 2020 WEBRip 2160p", 5)]
    [InlineData("Artist - Title 2020 WEB.DL.480p", 2)]
    public void Parses_quality_from_title(string title, int expectedQualityId)
    {
        var parsed = _sut.Parse(title);
        parsed.Quality.Id.Should().Be(expectedQualityId);
    }

    [Fact]
    public void Quality_defaults_to_unknown_when_unrecognised()
    {
        var parsed = _sut.Parse("Daft Punk - Around the World");
        parsed.Quality.Should().Be(Quality.Unknown);
    }

    [Theory]
    [InlineData("Daft Punk - Around the World (1997) WEB-DL 1080p H.264-FOOL", "FOOL")]
    [InlineData("Daft Punk - Around the World 1997 1080p WEB-DL [GROUP]", "GROUP")]
    [InlineData("Madonna - Material Girl (1984) BluRay 1080p H264 GROUP2", "GROUP2")]
    public void Extracts_release_group(string title, string expectedGroup)
    {
        var parsed = _sut.Parse(title);
        parsed.ReleaseGroup.Should().Be(expectedGroup);
    }

    [Fact]
    public void Handles_dotted_scene_release_names()
    {
        // Scene-style dotted names don't carry an explicit " - " between artist and title;
        // disambiguating artist vs. title boundary requires a catalog lookup (a later phase).
        // For Phase 1 the parser must at least pull year, quality, and release group correctly.
        var parsed = _sut.Parse("Daft.Punk.Around.the.World.1997.WEB-DL.1080p.H264-FOOL");
        parsed.Year.Should().Be(1997);
        parsed.Quality.Should().Be(Quality.Webdl1080p);
        parsed.ReleaseGroup.Should().Be("FOOL");
        parsed.Title.Should().Contain("Daft Punk").And.Contain("Around the World");
    }

    [Fact]
    public void Trims_leading_and_trailing_whitespace_in_artist_and_title()
    {
        var parsed = _sut.Parse("   Daft Punk   -   Around the World   (1997)   ");
        parsed.ArtistName.Should().Be("Daft Punk");
        parsed.Title.Should().Be("Around the World");
    }

    private static readonly string[] ExpectedProperRepack = ["PROPER", "REPACK"];

    [Fact]
    public void Tags_collect_remaining_known_tokens()
    {
        var parsed = _sut.Parse("Daft Punk - Around the World (1997) WEB-DL 1080p PROPER REPACK");
        parsed.Tags.Should().Contain(ExpectedProperRepack);
    }

    [Fact]
    public void Missing_artist_title_separator_keeps_full_text_as_title()
    {
        var parsed = _sut.Parse("AroundTheWorld1997WEB-DL1080p");
        parsed.ArtistName.Should().BeNull();
        parsed.Title.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_input_yields_safe_parsed_info(string input)
    {
        var parsed = _sut.Parse(input);
        parsed.ArtistName.Should().BeNull();
        parsed.Title.Should().Be(string.Empty);
        parsed.Year.Should().BeNull();
        parsed.Quality.Should().Be(Quality.Unknown);
        parsed.ReleaseGroup.Should().BeNull();
    }

    [Theory]
    [InlineData("Artist - Title 1899", null)]
    [InlineData("Artist - Title 2099", 2099)]
    [InlineData("Artist - Title 1900", 1900)]
    public void Year_must_be_in_realistic_range(string title, int? expectedYear)
    {
        var parsed = _sut.Parse(title);
        parsed.Year.Should().Be(expectedYear);
    }
}
