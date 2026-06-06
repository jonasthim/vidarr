using Vidarr.Contracts.Models;
using Vidarr.Naming;

namespace Vidarr.Naming.Tests;

public class NamingServiceTests
{
    private readonly INamingService _sut = new NamingService();

    [Fact]
    public void Default_template_produces_expected_path()
    {
        var input = new NamingInput(
            ArtistName: "Daft Punk",
            Title: "Around the World",
            Year: 1997,
            Quality: Quality.Webdl1080p,
            Extension: "mkv");

        var path = _sut.BuildRelativePath(input, NamingConfig.Default);

        path.Should().Be(Path.Combine(
            "Daft Punk",
            "Daft Punk - Around the World (1997) [WEBDL-1080p].mkv"));
    }

    [Fact]
    public void Missing_year_token_collapses_cleanly()
    {
        var input = new NamingInput(
            ArtistName: "Daft Punk",
            Title: "Around the World",
            Year: null,
            Quality: Quality.Webdl1080p,
            Extension: "mkv");

        var path = _sut.BuildRelativePath(input, NamingConfig.Default);

        path.Should().Be(Path.Combine(
            "Daft Punk",
            "Daft Punk - Around the World [WEBDL-1080p].mkv"));
    }

    [Fact]
    public void Illegal_characters_are_replaced_with_configured_char()
    {
        var input = new NamingInput(
            ArtistName: "AC/DC",
            Title: "T.N.T.",
            Year: 1975,
            Quality: Quality.Webdl720p,
            Extension: "mkv");

        var path = _sut.BuildRelativePath(input, NamingConfig.Default);

        path.Should().Be(Path.Combine(
            "AC_DC",
            "AC_DC - T.N.T. (1975) [WEBDL-720p].mkv"));
    }

    [Fact]
    public void Custom_template_renders_using_token_values()
    {
        var config = NamingConfig.Default with
        {
            FileTemplate = "{Title} ({Year}).{Quality Full}",
            ArtistFolderTemplate = "{Artist Name}",
        };
        var input = new NamingInput(
            ArtistName: "Daft Punk",
            Title: "One More Time",
            Year: 2000,
            Quality: Quality.Webdl2160p,
            Extension: "mkv");

        var path = _sut.BuildRelativePath(input, config);

        path.Should().Be(Path.Combine(
            "Daft Punk",
            "One More Time (2000).WEBDL-2160p.mkv"));
    }

    [Fact]
    public void Extra_tokens_are_substitutable()
    {
        var config = NamingConfig.Default with
        {
            FileTemplate = "{Artist Name} - {Title} ({Year}) [{Source}]",
        };
        var input = new NamingInput(
            ArtistName: "Aphex Twin",
            Title: "Windowlicker",
            Year: 1999,
            Quality: Quality.Webdl1080p,
            Extension: "mkv",
            ExtraTokens: new Dictionary<string, string> { ["Source"] = "VEVO" });

        var path = _sut.BuildRelativePath(input, config);

        path.Should().Be(Path.Combine(
            "Aphex Twin",
            "Aphex Twin - Windowlicker (1999) [VEVO].mkv"));
    }

    [Fact]
    public void Unknown_tokens_become_empty_string()
    {
        var config = NamingConfig.Default with
        {
            FileTemplate = "{Artist Name} - {Title} {Director}",
        };
        var input = new NamingInput(
            ArtistName: "Aphex Twin",
            Title: "Windowlicker",
            Year: 1999,
            Quality: Quality.Webdl1080p,
            Extension: "mkv");

        var path = _sut.BuildRelativePath(input, config);

        path.Should().EndWith("Aphex Twin - Windowlicker.mkv");
    }

    [Fact]
    public void Extension_without_dot_is_normalised()
    {
        var input = new NamingInput("A", "B", 2000, Quality.Webdl720p, "mp4");
        var path = _sut.BuildRelativePath(input, NamingConfig.Default);
        path.Should().EndWith(".mp4");
    }

    [Fact]
    public void Extension_with_dot_is_not_double_dotted()
    {
        var input = new NamingInput("A", "B", 2000, Quality.Webdl720p, ".mp4");
        var path = _sut.BuildRelativePath(input, NamingConfig.Default);
        path.Should().EndWith(".mp4").And.NotEndWith("..mp4");
    }

    [Fact]
    public void Empty_extension_produces_no_trailing_dot()
    {
        var input = new NamingInput("A", "B", 2000, Quality.Webdl720p, string.Empty);
        var path = _sut.BuildRelativePath(input, NamingConfig.Default);
        path.Should().NotEndWith(".");
    }

    [Fact]
    public void Whitespace_around_token_braces_is_tolerated()
    {
        var config = NamingConfig.Default with
        {
            FileTemplate = "{ Artist Name } - { Title }",
        };
        var input = new NamingInput("Daft Punk", "Da Funk", 1995, Quality.Webdl720p, "mkv");
        var path = _sut.BuildRelativePath(input, config);
        path.Should().Contain("Daft Punk - Da Funk");
    }

    [Fact]
    public void Token_lookup_is_case_insensitive()
    {
        var config = NamingConfig.Default with
        {
            FileTemplate = "{ARTIST NAME} - {title}",
        };
        var input = new NamingInput("Daft Punk", "Da Funk", 1995, Quality.Webdl720p, "mkv");
        var path = _sut.BuildRelativePath(input, config);
        path.Should().Contain("Daft Punk - Da Funk");
    }

    [Fact]
    public void Disabling_illegal_replacement_keeps_originals()
    {
        var config = NamingConfig.Default with { ReplaceIllegalCharacters = false };
        var input = new NamingInput("AC/DC", "T:N:T", 1975, Quality.Webdl720p, "mkv");
        var path = _sut.BuildRelativePath(input, config);
        path.Should().Contain("AC/DC");
        path.Should().Contain("T:N:T");
    }
}
