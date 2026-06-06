using Vidarr.Contracts.Models;
using Vidarr.Decision.CustomFormats;

namespace Vidarr.Decision.Tests.CustomFormats;

public class CustomFormatSpecsTests
{
    private static CustomFormatSpecContext Build(
        string title = "Daft Punk - Around the World",
        Quality? quality = null,
        string? releaseGroup = "VEVO",
        long? size = 100_000_000,
        IReadOnlyDictionary<string, string>? extras = null,
        IReadOnlyDictionary<string, string>? flags = null,
        IReadOnlyList<string>? tags = null) =>
        new(
            Release: new ReleaseInfo(
                Title: title,
                SourceUrl: new Uri("https://example.com/r"),
                Magnet: null,
                SizeBytes: size,
                PublishedAt: DateTimeOffset.UtcNow,
                Age: TimeSpan.Zero,
                Seeders: null,
                Leechers: null,
                Protocol: DownloadProtocol.Streaming,
                IndexerName: "YouTube",
                IndexerCategory: "6030",
                ExtraMetadata: extras ?? new Dictionary<string, string>()),
            Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997,
                quality ?? Quality.Webdl1080p, releaseGroup, tags ?? []),
            IndexerFlags: flags ?? new Dictionary<string, string>());

    [Fact]
    public void ReleaseTitle_matches_pattern_case_insensitive()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"ReleaseTitleSpecification","required":true,"fields":{"value":"daft\\s+punk"}}]""");
        specs.Should().ContainSingle().Which.Matches(Build()).Should().BeTrue();
    }

    [Fact]
    public void ReleaseTitle_invalid_regex_does_not_throw()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"ReleaseTitleSpecification","fields":{"value":"("}}]""");
        specs.Should().ContainSingle().Which.Matches(Build()).Should().BeFalse();
    }

    [Fact]
    public void ReleaseTitle_empty_pattern_does_not_match()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"ReleaseTitleSpecification","fields":{}}]""");
        specs.Should().ContainSingle().Which.Matches(Build()).Should().BeFalse();
    }

    [Fact]
    public void ReleaseGroup_matches_parsed_group()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"ReleaseGroupSpecification","fields":{"value":"VEVO"}}]""");
        specs[0].Matches(Build()).Should().BeTrue();
        specs[0].Matches(Build(releaseGroup: null)).Should().BeFalse();
        specs[0].Matches(Build(releaseGroup: "scenegrp")).Should().BeFalse();
    }

    [Fact]
    public void IndexerFlag_matches_when_key_present()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"IndexerFlagSpecification","fields":{"flagKey":"freeleech"}}]""");
        specs[0].Matches(Build(flags: new Dictionary<string, string> { ["freeleech"] = "true" })).Should().BeTrue();
        specs[0].Matches(Build()).Should().BeFalse();
    }

    [Fact]
    public void IndexerFlag_with_expected_value_must_match_exactly()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"IndexerFlagSpecification","fields":{"flagKey":"category","value":"music"}}]""");
        specs[0].Matches(Build(flags: new Dictionary<string, string> { ["category"] = "music" })).Should().BeTrue();
        specs[0].Matches(Build(flags: new Dictionary<string, string> { ["category"] = "tv" })).Should().BeFalse();
    }

    [Fact]
    public void Source_matches_enum_name_case_insensitive()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"SourceSpecification","fields":{"source":"webdl"}}]""");
        specs[0].Matches(Build()).Should().BeTrue();
        specs[0].Matches(Build(quality: Quality.Bluray1080p)).Should().BeFalse();
    }

    [Fact]
    public void Resolution_matches_enum_name()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"ResolutionSpecification","fields":{"resolution":"R1080p"}}]""");
        specs[0].Matches(Build()).Should().BeTrue();
        specs[0].Matches(Build(quality: Quality.Webdl720p)).Should().BeFalse();
    }

    [Fact]
    public void Language_matches_parsed_tag_or_release_extra()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"LanguageSpecification","fields":{"language":"en"}}]""");
        specs[0].Matches(Build(tags: ["EN"])).Should().BeTrue();
        specs[0].Matches(Build(extras: new Dictionary<string, string> { ["language"] = "en" })).Should().BeTrue();
        specs[0].Matches(Build()).Should().BeFalse();
    }

    [Fact]
    public void Size_matches_min_max_window()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"SizeSpecification","fields":{"minBytes":50000000,"maxBytes":150000000}}]""");
        specs[0].Matches(Build(size: 100_000_000)).Should().BeTrue();
        specs[0].Matches(Build(size: 10_000_000)).Should().BeFalse();   // below min
        specs[0].Matches(Build(size: 999_999_999)).Should().BeFalse();   // above max
        specs[0].Matches(Build(size: null)).Should().BeFalse();
    }

    [Fact]
    public void Size_with_no_bounds_matches_anything_with_a_size()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"SizeSpecification","fields":{}}]""");
        specs[0].Matches(Build(size: 100_000)).Should().BeTrue();
        specs[0].Matches(Build(size: null)).Should().BeFalse();
    }

    [Fact]
    public void YouTubeChannel_matches_channel_id_exact()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"YouTubeChannelSpecification","fields":{"channel":"UCdaftVEVO"}}]""");
        specs[0].Matches(Build(extras: new Dictionary<string, string> { ["channelId"] = "UCdaftVEVO" })).Should().BeTrue();
    }

    [Fact]
    public void YouTubeChannel_falls_back_to_channel_title_substring()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"YouTubeChannelSpecification","fields":{"channel":"VEVO"}}]""");
        specs[0].Matches(Build(extras: new Dictionary<string, string> { ["channelTitle"] = "DaftPunkVEVO" })).Should().BeTrue();
        specs[0].Matches(Build(extras: new Dictionary<string, string> { ["channelTitle"] = "Some Random" })).Should().BeFalse();
    }

    [Fact]
    public void Negate_inverts_the_match()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"ReleaseTitleSpecification","negate":true,"fields":{"value":"x265"}}]""");
        specs[0].Matches(Build(title: "Daft Punk - h264 release")).Should().BeTrue();    // doesn't contain x265 → inverted true
        specs[0].Matches(Build(title: "Daft Punk - x265 rip")).Should().BeFalse();        // contains x265 → inverted false
    }

    [Fact]
    public void Required_and_Negate_flags_round_trip_via_parser()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"SourceSpecification","required":true,"negate":false,"fields":{"source":"Webdl"}}]""");
        specs[0].Required.Should().BeTrue();
        specs[0].Negate.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("not-json")]
    [InlineData(null)]
    public void Parser_tolerates_garbage_input(string? input) =>
        CustomFormatSpecParser.Parse(input!).Should().BeEmpty();

    [Fact]
    public void Parser_drops_unknown_implementations_silently()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"MysterySpecification","fields":{"x":"y"}},{"implementation":"SourceSpecification","fields":{"source":"Webdl"}}]""");
        specs.Should().ContainSingle().Which.Implementation.Should().Be("SourceSpecification");
    }

    [Fact]
    public void Parser_drops_specs_with_empty_implementation()
    {
        var specs = CustomFormatSpecParser.Parse(
            """[{"implementation":"","fields":{}},{"implementation":"SourceSpecification","fields":{"source":"Webdl"}}]""");
        specs.Should().ContainSingle();
    }
}
