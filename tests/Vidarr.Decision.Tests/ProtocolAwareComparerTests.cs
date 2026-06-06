using Vidarr.Contracts.Models;
using Vidarr.Decision;

namespace Vidarr.Decision.Tests;

public class ProtocolAwareComparerTests
{
    private static QualityProfile Profile() => new(
        AllowedQualities: [Quality.Webdl720p, Quality.Webdl1080p, Quality.Webdl2160p],
        Cutoff: Quality.Webdl1080p,
        UpgradeAllowed: true);

    private static RemoteRelease Sample(
        Quality? quality = null,
        DownloadProtocol protocol = DownloadProtocol.Usenet,
        int? seeders = null,
        TimeSpan? age = null,
        long? size = 1_000_000) =>
        new(
            Info: new ReleaseInfo(
                Title: "X",
                SourceUrl: new Uri("https://example.com/r"),
                Magnet: null,
                SizeBytes: size,
                PublishedAt: DateTimeOffset.UtcNow,
                Age: age,
                Seeders: seeders,
                Leechers: null,
                Protocol: protocol,
                IndexerName: "I",
                IndexerCategory: "6030",
                ExtraMetadata: new Dictionary<string, string>()),
            Parsed: new ParsedReleaseInfo("A", "T", 2024, quality ?? Quality.Webdl1080p, null, []),
            Score: 0,
            RejectionReasons: [],
            MatchedMusicVideoIds: []);

    [Fact]
    public void Protocol_preference_default_prefers_usenet_over_torrent_over_streaming()
    {
        var sut = new DefaultReleaseComparer(Profile());
        var u = Sample(protocol: DownloadProtocol.Usenet);
        var t = Sample(protocol: DownloadProtocol.Torrent);
        var s = Sample(protocol: DownloadProtocol.Streaming);
        sut.Compare(u, t).Should().BeLessThan(0);
        sut.Compare(t, s).Should().BeLessThan(0);
    }

    [Fact]
    public void Protocol_preference_is_configurable()
    {
        var pref = new ProtocolPreference([DownloadProtocol.Streaming, DownloadProtocol.Torrent, DownloadProtocol.Usenet]);
        var sut = new DefaultReleaseComparer(Profile(), pref);
        var u = Sample(protocol: DownloadProtocol.Usenet);
        var s = Sample(protocol: DownloadProtocol.Streaming);
        sut.Compare(s, u).Should().BeLessThan(0); // streaming wins now
    }

    [Fact]
    public void Usenet_ties_break_by_age_younger_wins()
    {
        var sut = new DefaultReleaseComparer(Profile());
        var fresh = Sample(protocol: DownloadProtocol.Usenet, age: TimeSpan.FromHours(2));
        var stale = Sample(protocol: DownloadProtocol.Usenet, age: TimeSpan.FromDays(30));
        sut.Compare(fresh, stale).Should().BeLessThan(0);
    }

    [Fact]
    public void Torrent_ties_break_by_seeder_count_higher_wins()
    {
        var sut = new DefaultReleaseComparer(Profile());
        var popular = Sample(protocol: DownloadProtocol.Torrent, seeders: 500);
        var lonely = Sample(protocol: DownloadProtocol.Torrent, seeders: 2);
        sut.Compare(popular, lonely).Should().BeLessThan(0);
    }

    [Fact]
    public void Mixed_protocol_pairs_do_not_apply_protocol_specific_tie_break()
    {
        var sut = new DefaultReleaseComparer(Profile());
        // Same quality, mixed protocol: protocol preference order is the only thing that matters.
        var olderUsenet = Sample(protocol: DownloadProtocol.Usenet, age: TimeSpan.FromDays(60));
        var freshTorrent = Sample(protocol: DownloadProtocol.Torrent, age: TimeSpan.FromHours(1), seeders: 9999);
        sut.Compare(olderUsenet, freshTorrent).Should().BeLessThan(0); // Usenet wins by preference
    }

    [Fact]
    public void Size_remains_final_tie_break()
    {
        var sut = new DefaultReleaseComparer(Profile());
        var big = Sample(size: 100_000_000);
        var small = Sample(size: 1_000_000);
        sut.Compare(big, small).Should().BeLessThan(0);
    }

    [Fact]
    public void Rank_returns_after_known_protocols()
    {
        ProtocolPreference.Default.Rank(DownloadProtocol.Unknown).Should().Be(ProtocolPreference.Default.Order.Count);
    }
}
