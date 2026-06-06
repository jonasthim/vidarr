using Vidarr.Contracts.Models;
using Vidarr.Decision;

namespace Vidarr.Decision.Tests;

public class CustomFormatAwareComparerTests
{
    private static QualityProfile Profile() => new(
        AllowedQualities: [Quality.Webdl720p, Quality.Webdl1080p, Quality.Webdl2160p],
        Cutoff: Quality.Webdl1080p,
        UpgradeAllowed: true);

    private static RemoteRelease Sample(
        Quality? quality = null,
        int score = 0,
        DownloadProtocol protocol = DownloadProtocol.Streaming,
        int? seeders = null,
        TimeSpan? age = null,
        long size = 1_000_000,
        string indexerName = "I") =>
        new(
            Info: new ReleaseInfo("X", new Uri("https://example.com"), null, size, DateTimeOffset.UtcNow,
                age ?? TimeSpan.Zero, seeders, null, protocol, indexerName, "6030",
                new Dictionary<string, string>()),
            Parsed: new ParsedReleaseInfo("A", "T", 2024, quality ?? Quality.Webdl1080p, null, []),
            Score: score,
            RejectionReasons: [],
            MatchedMusicVideoIds: []);

    [Fact]
    public void Custom_format_score_beats_protocol_preference_when_quality_equal()
    {
        var sut = new DefaultReleaseComparer(Profile());
        var highScore = Sample(score: 50, protocol: DownloadProtocol.Streaming);
        var lowScore = Sample(score: 0, protocol: DownloadProtocol.Usenet);
        sut.Compare(highScore, lowScore).Should().BeLessThan(0);
    }

    [Fact]
    public void Quality_still_beats_custom_format_score()
    {
        var sut = new DefaultReleaseComparer(Profile());
        var higherQ = Sample(quality: Quality.Webdl2160p, score: 0);
        var lowerQHighScore = Sample(quality: Quality.Webdl720p, score: 999);
        sut.Compare(higherQ, lowerQHighScore).Should().BeLessThan(0);
    }

    [Fact]
    public void Indexer_priority_breaks_ties_lower_number_wins()
    {
        var priority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["FastIndexer"] = 1,
            ["SlowIndexer"] = 25,
        };
        var sut = new DefaultReleaseComparer(Profile(), indexerPriority: priority);
        var fast = Sample(indexerName: "FastIndexer", protocol: DownloadProtocol.Streaming);
        var slow = Sample(indexerName: "SlowIndexer", protocol: DownloadProtocol.Streaming);
        sut.Compare(fast, slow).Should().BeLessThan(0);
    }

    [Fact]
    public void Comparer_ordering_remains_stable_when_everything_ties_to_size()
    {
        var sut = new DefaultReleaseComparer(Profile());
        var big = Sample(size: 100_000_000);
        var small = Sample(size: 1_000);
        sut.Compare(big, small).Should().BeLessThan(0);
    }
}
