using Vidarr.Contracts.Models;
using Vidarr.Decision;

namespace Vidarr.Decision.Tests;

public class MinFormatScoreAndRequiredSpecsTests
{
    private static readonly ReleaseInfo SampleRelease = new(
        Title: "Daft Punk - Around the World",
        SourceUrl: new Uri("https://example.com/r"),
        Magnet: null,
        SizeBytes: 100_000_000,
        PublishedAt: DateTimeOffset.UtcNow,
        Age: TimeSpan.Zero,
        Seeders: null,
        Leechers: null,
        Protocol: DownloadProtocol.Streaming,
        IndexerName: "YouTube",
        IndexerCategory: "6030",
        ExtraMetadata: new Dictionary<string, string>());

    private static ParsedReleaseInfo SampleParsed() =>
        new("Daft Punk", "Around the World", 1997, Quality.Webdl1080p, "VEVO", []);

    private static QualityProfile Profile(int minScore = 0) => new(
        AllowedQualities: [Quality.Webdl1080p],
        Cutoff: Quality.Webdl1080p,
        UpgradeAllowed: true,
        MinFormatScore: minScore);

    [Fact]
    public void MinFormatScore_passes_when_threshold_zero()
    {
        var sut = new MinFormatScoreSpec();
        var ctx = new DecisionContext(1, 2, null, Profile(0), new HashSet<string>(), CurrentFormatScore: -5);
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out _).Should().BeTrue();
    }

    [Fact]
    public void MinFormatScore_rejects_when_score_below_threshold()
    {
        var sut = new MinFormatScoreSpec();
        var ctx = new DecisionContext(1, 2, null, Profile(50), new HashSet<string>(), CurrentFormatScore: 10);
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out var reason).Should().BeFalse();
        reason.Should().Contain("10").And.Contain("50");
    }

    [Fact]
    public void MinFormatScore_passes_when_score_at_or_above_threshold()
    {
        var sut = new MinFormatScoreSpec();
        var ctx = new DecisionContext(1, 2, null, Profile(50), new HashSet<string>(), CurrentFormatScore: 50);
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out _).Should().BeTrue();
    }

    [Fact]
    public void CustomFormatRequired_passes_when_no_required_formats()
    {
        var sut = new CustomFormatRequiredSpec();
        var ctx = new DecisionContext(1, 2, null, Profile(), new HashSet<string>());
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out _).Should().BeTrue();
    }

    [Fact]
    public void CustomFormatRequired_rejects_when_a_required_format_did_not_match()
    {
        var sut = new CustomFormatRequiredSpec();
        var ctx = new DecisionContext(1, 2, null, Profile(), new HashSet<string>(),
            MatchedFormatIds: new HashSet<int> { 1 },
            RequiredFormatIds: new HashSet<int> { 1, 2 });
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out var reason).Should().BeFalse();
        reason.Should().Contain("2");
    }

    [Fact]
    public void CustomFormatRequired_passes_when_all_required_formats_matched()
    {
        var sut = new CustomFormatRequiredSpec();
        var ctx = new DecisionContext(1, 2, null, Profile(), new HashSet<string>(),
            MatchedFormatIds: new HashSet<int> { 1, 2, 3 },
            RequiredFormatIds: new HashSet<int> { 1, 2 });
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out _).Should().BeTrue();
    }
}
