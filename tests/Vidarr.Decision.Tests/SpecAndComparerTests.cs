using Vidarr.Contracts.Models;
using Vidarr.Decision;

namespace Vidarr.Decision.Tests;

public class SpecAndComparerTests
{
    private static readonly ReleaseInfo SampleRelease = new(
        Title: "Daft Punk - Around the World (1997) WEBDL-1080p",
        SourceUrl: new Uri("https://example.com/r/1"),
        Magnet: null,
        SizeBytes: 100_000_000,
        PublishedAt: DateTimeOffset.UtcNow,
        Age: TimeSpan.Zero,
        Seeders: 5,
        Leechers: 1,
        Protocol: DownloadProtocol.Torrent,
        IndexerName: "X",
        IndexerCategory: "6030",
        ExtraMetadata: new Dictionary<string, string>());

    private static ParsedReleaseInfo SampleParsed(Quality? q = null) =>
        new("Daft Punk", "Around the World", 1997, q ?? Quality.Webdl1080p, "GROUP", []);

    private static QualityProfile DefaultProfile(Quality? cutoff = null, bool upgradeAllowed = true) =>
        new(
            AllowedQualities: [Quality.Webdl720p, Quality.Webdl1080p, Quality.Webdl2160p],
            Cutoff: cutoff ?? Quality.Webdl1080p,
            UpgradeAllowed: upgradeAllowed);

    private static DecisionContext BuildCtx(QualityProfile? profile = null, Quality? current = null, params string[] blocklist) =>
        new(1, 2, current, profile ?? DefaultProfile(), new HashSet<string>(blocklist));

    [Fact]
    public void AlreadyImportedSpec_rejects_when_already_imported_and_upgrades_disabled()
    {
        var sut = new AlreadyImportedSpec();
        var ctx = BuildCtx(profile: DefaultProfile(upgradeAllowed: false), current: Quality.Webdl1080p);
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out var reason).Should().BeFalse();
        reason.Should().Contain("upgrades disallowed");
    }

    [Fact]
    public void AlreadyImportedSpec_allows_when_upgrades_enabled()
    {
        var sut = new AlreadyImportedSpec();
        var ctx = BuildCtx(current: Quality.Webdl720p);
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out _).Should().BeTrue();
    }

    [Fact]
    public void BlocklistedSpec_rejects_when_title_listed()
    {
        var sut = new BlocklistedSpec();
        var ctx = BuildCtx(blocklist: SampleRelease.Title);
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out var reason).Should().BeFalse();
        reason.Should().Contain("blocklisted");
    }

    [Fact]
    public void BlocklistedSpec_passes_when_not_listed()
    {
        var sut = new BlocklistedSpec();
        var ctx = BuildCtx();
        sut.IsSatisfied(SampleRelease, SampleParsed(), ctx, out _).Should().BeTrue();
    }

    [Fact]
    public void QualityAllowedSpec_rejects_disallowed_quality()
    {
        var sut = new QualityAllowedSpec();
        var ctx = BuildCtx();
        sut.IsSatisfied(SampleRelease, SampleParsed(Quality.Hdtv720p), ctx, out var reason).Should().BeFalse();
        reason.Should().Contain("not in profile");
    }

    [Fact]
    public void QualityAllowedSpec_passes_allowed_quality()
    {
        var sut = new QualityAllowedSpec();
        sut.IsSatisfied(SampleRelease, SampleParsed(), BuildCtx(), out _).Should().BeTrue();
    }

    [Fact]
    public void MinSizeSpec_rejects_when_below_min()
    {
        var sut = new MinSizeSpec();
        var profile = DefaultProfile() with { MinSizeBytes = 200_000_000 };
        sut.IsSatisfied(SampleRelease, SampleParsed(), BuildCtx(profile), out var reason).Should().BeFalse();
        reason.Should().Contain("below minimum");
    }

    [Fact]
    public void MinSizeSpec_passes_when_no_min_or_above()
    {
        var sut = new MinSizeSpec();
        sut.IsSatisfied(SampleRelease, SampleParsed(), BuildCtx(), out _).Should().BeTrue();
    }

    [Fact]
    public void MaxSizeSpec_rejects_when_above_max()
    {
        var sut = new MaxSizeSpec();
        var profile = DefaultProfile() with { MaxSizeBytes = 50_000_000 };
        sut.IsSatisfied(SampleRelease, SampleParsed(), BuildCtx(profile), out var reason).Should().BeFalse();
        reason.Should().Contain("exceeds maximum");
    }

    [Fact]
    public void MaxSizeSpec_passes_when_no_max_or_below()
    {
        var sut = new MaxSizeSpec();
        sut.IsSatisfied(SampleRelease, SampleParsed(), BuildCtx(), out _).Should().BeTrue();
    }

    [Fact]
    public void UpgradeAllowedSpec_rejects_equal_or_lower_quality_replacement()
    {
        var sut = new UpgradeAllowedSpec();
        var ctx = BuildCtx(current: Quality.Webdl1080p);
        sut.IsSatisfied(SampleRelease, SampleParsed(Quality.Webdl720p), ctx, out var reason).Should().BeFalse();
        reason.Should().Contain("not worse than candidate");
    }

    [Fact]
    public void UpgradeAllowedSpec_passes_when_no_current_file()
    {
        var sut = new UpgradeAllowedSpec();
        sut.IsSatisfied(SampleRelease, SampleParsed(), BuildCtx(), out _).Should().BeTrue();
    }

    [Fact]
    public void CutoffMetSpec_rejects_when_cutoff_satisfied()
    {
        var sut = new CutoffMetSpec();
        var profile = DefaultProfile(cutoff: Quality.Webdl720p);
        var ctx = BuildCtx(profile, current: Quality.Webdl1080p);
        sut.IsSatisfied(SampleRelease, SampleParsed(Quality.Webdl2160p), ctx, out var reason).Should().BeFalse();
        reason.Should().Contain("already met");
    }

    [Fact]
    public void CutoffMetSpec_passes_when_no_current_file()
    {
        var sut = new CutoffMetSpec();
        sut.IsSatisfied(SampleRelease, SampleParsed(), BuildCtx(), out _).Should().BeTrue();
    }

    [Fact]
    public void Comparer_ranks_higher_quality_first()
    {
        var profile = DefaultProfile();
        var comparer = new DefaultReleaseComparer(profile);
        var lower = new RemoteRelease(SampleRelease, SampleParsed(Quality.Webdl720p), 0, [], []);
        var higher = new RemoteRelease(SampleRelease, SampleParsed(Quality.Webdl2160p), 0, [], []);
        comparer.Compare(higher, lower).Should().BeLessThan(0);
    }

    [Fact]
    public void Comparer_pushes_rejected_releases_last()
    {
        var profile = DefaultProfile();
        var comparer = new DefaultReleaseComparer(profile);
        var rejected = new RemoteRelease(SampleRelease, SampleParsed(), 0, ["nope"], []);
        var accepted = new RemoteRelease(SampleRelease, SampleParsed(), 0, [], []);
        comparer.Compare(accepted, rejected).Should().BeLessThan(0);
    }

    [Fact]
    public void Comparer_handles_null_inputs_consistently()
    {
        var comparer = new DefaultReleaseComparer(DefaultProfile());
        comparer.Compare(null, null).Should().Be(0);
        comparer.Compare(null, new RemoteRelease(SampleRelease, SampleParsed(), 0, [], [])).Should().BeGreaterThan(0);
        comparer.Compare(new RemoteRelease(SampleRelease, SampleParsed(), 0, [], []), null).Should().BeLessThan(0);
    }

    [Fact]
    public void Comparer_breaks_ties_with_seeders_then_size()
    {
        var profile = DefaultProfile();
        var comparer = new DefaultReleaseComparer(profile);
        var moreSeeders = SampleRelease with { Seeders = 100 };
        var fewerSeeders = SampleRelease with { Seeders = 1 };
        var a = new RemoteRelease(moreSeeders, SampleParsed(), 0, [], []);
        var b = new RemoteRelease(fewerSeeders, SampleParsed(), 0, [], []);
        comparer.Compare(a, b).Should().BeLessThan(0);

        var equalSeeders1 = SampleRelease with { Seeders = 10, SizeBytes = 1_000 };
        var equalSeeders2 = SampleRelease with { Seeders = 10, SizeBytes = 100 };
        var x = new RemoteRelease(equalSeeders1, SampleParsed(), 0, [], []);
        var y = new RemoteRelease(equalSeeders2, SampleParsed(), 0, [], []);
        comparer.Compare(x, y).Should().BeLessThan(0);
    }

    [Fact]
    public void DecisionEngine_runs_parser_specs_and_orders()
    {
        var parser = new ReleaseParser();
        var profile = DefaultProfile();
        var engine = new DecisionEngine(
            parser,
            [new QualityAllowedSpec(), new MinSizeSpec()],
            new DefaultReleaseComparer(profile));

        var a = SampleRelease with { Title = "Daft Punk - Around the World (1997) WEBDL 720p", SizeBytes = 50_000_000 };
        var b = SampleRelease with { Title = "Daft Punk - Around the World (1997) HDTV 720p", SizeBytes = 50_000_000 };
        var ctx = new DecisionContext(1, 2, null, profile, new HashSet<string>());

        var decided = engine.Decide([a, b], ctx);
        decided.Should().HaveCount(2);
        decided[0].Parsed.Quality.Should().Be(Quality.Webdl720p);
        decided[1].RejectionReasons.Should().NotBeEmpty();
    }
}
