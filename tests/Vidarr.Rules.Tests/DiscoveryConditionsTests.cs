using Vidarr.Contracts.Models;
using Vidarr.Rules;

namespace Vidarr.Rules.Tests;

public class DiscoveryConditionsTests
{
    private static DiscoveryContext Ctx(int? year = 2024, IReadOnlyList<string>? genres = null,
        string? country = "US", MusicVideoType type = MusicVideoType.Official) =>
        new(year, genres ?? ["Synthwave"], country, type);

    [Fact]
    public void GenreIn_matches_case_insensitive()
    {
        var cond = DiscoveryConditionParser.Parse("""[{"type":"GenreIn","values":["synthwave"]}]""")
            .Should().ContainSingle().Which;
        cond.Matches(Ctx()).Should().BeTrue();
        cond.Matches(Ctx(genres: ["pop"])).Should().BeFalse();
    }

    [Fact]
    public void YearGte_matches_year_at_or_above_threshold()
    {
        var c = DiscoveryConditionParser.Parse("""[{"type":"YearGte","value":2020}]""")[0];
        c.Matches(Ctx(year: 2024)).Should().BeTrue();
        c.Matches(Ctx(year: 2020)).Should().BeTrue();
        c.Matches(Ctx(year: 2019)).Should().BeFalse();
        c.Matches(Ctx(year: null)).Should().BeFalse();
    }

    [Fact]
    public void YearLte_matches_year_at_or_below_threshold()
    {
        var c = DiscoveryConditionParser.Parse("""[{"type":"YearLte","value":2020}]""")[0];
        c.Matches(Ctx(year: 2020)).Should().BeTrue();
        c.Matches(Ctx(year: 2019)).Should().BeTrue();
        c.Matches(Ctx(year: 2021)).Should().BeFalse();
    }

    [Theory]
    [InlineData(2020, 2020, true)]
    [InlineData(2020, 2029, true)]
    [InlineData(2020, 2030, false)]
    [InlineData(2020, 2019, false)]
    public void DecadeEq_matches_same_decade(int decade, int year, bool expected)
    {
        var c = DiscoveryConditionParser.Parse($$"""[{"type":"DecadeEq","value":{{decade}}}]""")[0];
        c.Matches(Ctx(year: year)).Should().Be(expected);
    }

    [Fact]
    public void TypeIn_matches_against_enum_name()
    {
        var c = DiscoveryConditionParser.Parse("""[{"type":"TypeIn","values":["Live","Official"]}]""")[0];
        c.Matches(Ctx(type: MusicVideoType.Live)).Should().BeTrue();
        c.Matches(Ctx(type: MusicVideoType.Lyric)).Should().BeFalse();
    }

    [Fact]
    public void CountryIn_matches_artist_country()
    {
        var c = DiscoveryConditionParser.Parse("""[{"type":"CountryIn","values":["US","UK"]}]""")[0];
        c.Matches(Ctx(country: "US")).Should().BeTrue();
        c.Matches(Ctx(country: "DE")).Should().BeFalse();
        c.Matches(Ctx(country: null)).Should().BeFalse();
    }

    [Fact]
    public void Multiple_conditions_act_as_logical_AND()
    {
        var conditions = DiscoveryConditionParser.Parse(
            """[{"type":"GenreIn","values":["Synthwave"]},{"type":"YearGte","value":2020}]""");
        conditions.All(c => c.Matches(Ctx(year: 2024))).Should().BeTrue();
        conditions.All(c => c.Matches(Ctx(year: 2010))).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("garbage")]
    [InlineData("null")]
    public void Parser_tolerates_garbage(string input) =>
        DiscoveryConditionParser.Parse(input).Should().BeEmpty();

    [Fact]
    public void Unknown_condition_types_are_silently_dropped()
    {
        var c = DiscoveryConditionParser.Parse(
            """[{"type":"Mystery","values":["x"]},{"type":"GenreIn","values":["Synthwave"]}]""");
        c.Should().ContainSingle().Which.Type.Should().Be("GenreIn");
    }

    [Fact]
    public void Year_conditions_missing_value_are_dropped()
    {
        var c = DiscoveryConditionParser.Parse("""[{"type":"YearGte"}]""");
        c.Should().BeEmpty();
    }

    [Fact]
    public void Discovery_action_parses_with_monitor_mode_and_tags()
    {
        var action = DiscoveryActionParser.Parse(
            """{"qualityProfileId":3,"rootFolderPath":"/auto","monitorMode":"All","tags":[1,2]}""");
        action.QualityProfileId.Should().Be(3);
        action.RootFolderPath.Should().Be("/auto");
        action.MonitorMode.Should().Be(MonitorMode.All);
        action.Tags.Should().Equal(1, 2);
    }

    [Fact]
    public void Discovery_action_tolerates_missing_or_garbage_input()
    {
        DiscoveryActionParser.Parse("").MonitorMode.Should().BeNull();
        DiscoveryActionParser.Parse("nonsense").MonitorMode.Should().BeNull();
        DiscoveryActionParser.Parse("{}").QualityProfileId.Should().BeNull();
        DiscoveryActionParser.Parse("""{"monitorMode":"Garbage"}""").MonitorMode.Should().BeNull();
    }
}
