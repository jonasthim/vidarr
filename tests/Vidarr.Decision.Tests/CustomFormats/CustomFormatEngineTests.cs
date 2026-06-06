using Vidarr.Contracts.Models;
using Vidarr.Decision.CustomFormats;

namespace Vidarr.Decision.Tests.CustomFormats;

public class CustomFormatEngineTests
{
    private static CustomFormatSpecContext Ctx(
        string title = "Daft Punk - Around the World",
        Quality? quality = null) =>
        new(
            Release: new ReleaseInfo(title, new Uri("https://example.com"), null, 100, DateTimeOffset.UtcNow,
                TimeSpan.Zero, null, null, DownloadProtocol.Streaming, "YouTube", "6030",
                new Dictionary<string, string>()),
            Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997, quality ?? Quality.Webdl1080p, "VEVO", []),
            IndexerFlags: new Dictionary<string, string>());

    private static CustomFormatDefinition Format(int id, string name, string specsJson) =>
        new(id, name, CustomFormatSpecParser.Parse(specsJson));

    [Fact]
    public void Format_with_no_specs_never_matches()
    {
        var sut = new CustomFormatEngine();
        var result = sut.Evaluate(Ctx(), [new(1, "Empty", [])], new Dictionary<int, int> { [1] = 100 });
        result.Matches.Should().BeEmpty();
        result.TotalScore.Should().Be(0);
    }

    [Fact]
    public void Required_spec_must_match_for_format_to_match()
    {
        var sut = new CustomFormatEngine();
        var fmt = Format(1, "Webdl-Required",
            """[{"implementation":"SourceSpecification","required":true,"fields":{"source":"Webdl"}}]""");
        sut.Evaluate(Ctx(quality: Quality.Webdl1080p), [fmt], new Dictionary<int, int> { [1] = 10 })
            .Matches.Should().ContainSingle();
        sut.Evaluate(Ctx(quality: Quality.Bluray1080p), [fmt], new Dictionary<int, int> { [1] = 10 })
            .Matches.Should().BeEmpty();
    }

    [Fact]
    public void At_least_one_optional_spec_must_match_when_optionals_exist()
    {
        var sut = new CustomFormatEngine();
        const string specs = "[" +
            "{\"implementation\":\"SourceSpecification\",\"fields\":{\"source\":\"Webdl\"}}," +
            "{\"implementation\":\"SourceSpecification\",\"fields\":{\"source\":\"Bluray\"}}" +
            "]";
        var fmt = Format(1, "Any-Of", specs);
        sut.Evaluate(Ctx(quality: Quality.Webdl1080p), [fmt], new Dictionary<int, int> { [1] = 5 })
            .Matches.Should().ContainSingle();
        sut.Evaluate(Ctx(quality: Quality.Hdtv1080p), [fmt], new Dictionary<int, int> { [1] = 5 })
            .Matches.Should().BeEmpty();
    }

    [Fact]
    public void Required_and_optional_compose()
    {
        var sut = new CustomFormatEngine();
        const string specs = "[" +
            "{\"implementation\":\"SourceSpecification\",\"required\":true,\"fields\":{\"source\":\"Webdl\"}}," +
            "{\"implementation\":\"ReleaseGroupSpecification\",\"fields\":{\"value\":\"VEVO\"}}," +
            "{\"implementation\":\"ReleaseGroupSpecification\",\"fields\":{\"value\":\"OFFICIAL\"}}" +
            "]";
        var fmt = Format(1, "Webdl-AND-RG", specs);

        // required Webdl + at least one optional (VEVO matches) → format matches
        sut.Evaluate(Ctx(), [fmt], new Dictionary<int, int> { [1] = 10 }).Matches.Should().ContainSingle();

        // required Bluray fails → no match even if optional matches
        sut.Evaluate(Ctx(quality: Quality.Bluray1080p), [fmt], new Dictionary<int, int> { [1] = 10 })
            .Matches.Should().BeEmpty();
    }

    [Fact]
    public void Profile_score_sums_across_matched_formats()
    {
        var sut = new CustomFormatEngine();
        var fmt1 = Format(1, "Webdl",
            """[{"implementation":"SourceSpecification","required":true,"fields":{"source":"Webdl"}}]""");
        var fmt2 = Format(2, "1080p",
            """[{"implementation":"ResolutionSpecification","required":true,"fields":{"resolution":"R1080p"}}]""");
        var result = sut.Evaluate(Ctx(), [fmt1, fmt2],
            new Dictionary<int, int> { [1] = 10, [2] = 25 });
        result.Matches.Should().HaveCount(2);
        result.TotalScore.Should().Be(35);
    }

    [Fact]
    public void Profile_score_defaults_to_zero_when_match_has_no_explicit_score()
    {
        var sut = new CustomFormatEngine();
        var fmt = Format(1, "Webdl",
            """[{"implementation":"SourceSpecification","required":true,"fields":{"source":"Webdl"}}]""");
        var result = sut.Evaluate(Ctx(), [fmt], new Dictionary<int, int>());
        result.Matches.Should().ContainSingle().Which.Score.Should().Be(0);
        result.TotalScore.Should().Be(0);
    }

    [Fact]
    public void Negate_inverts_a_required_spec()
    {
        var sut = new CustomFormatEngine();
        var fmt = Format(1, "Not-x265",
            """[{"implementation":"ReleaseTitleSpecification","required":true,"negate":true,"fields":{"value":"x265"}}]""");
        sut.Evaluate(Ctx(title: "Daft Punk - h264 release"), [fmt],
            new Dictionary<int, int> { [1] = 5 }).Matches.Should().ContainSingle();
        sut.Evaluate(Ctx(title: "Daft Punk - x265 release"), [fmt],
            new Dictionary<int, int> { [1] = 5 }).Matches.Should().BeEmpty();
    }
}
