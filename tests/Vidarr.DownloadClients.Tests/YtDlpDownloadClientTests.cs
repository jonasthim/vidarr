using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Tests.Common;

namespace Vidarr.DownloadClients.Tests;

public class YtDlpDownloadClientTests
{
    private static (YtDlpDownloadClient Sut, FakeProcessRunner Procs, FakeFileSystem Fs) Build(
        IReadOnlyList<string>? progressLines = null,
        int exitCode = 0,
        string stderr = "")
    {
        var fs = new FakeFileSystem();
        var procs = new FakeProcessRunner();
        var result = new ProcessResult(exitCode, "", stderr, TimeSpan.FromMilliseconds(100));
        if (progressLines is null)
        {
            procs.SetDefault(result);
        }
        else
        {
            procs.WhenInvocation(_ => true, result, progressLines);
        }
        var settings = new YtDlpDownloadClientSettings(IncompleteFolder: "/tmp/incomplete");
        return (new YtDlpDownloadClient(1, "yt-dlp", settings, procs, fs), procs, fs);
    }

    private static RemoteRelease BuildRelease(string title = "Daft Punk - Around the World", string url = "https://www.youtube.com/watch?v=abc", long? size = 10_000_000) =>
        new(
            Info: new ReleaseInfo(title, new Uri(url), null, size, DateTimeOffset.UtcNow, null, null, null, DownloadProtocol.Streaming, "YouTube", "music-video", new Dictionary<string, string>()),
            Parsed: new ParsedReleaseInfo("Daft Punk", "Around the World", 1997, Quality.Webdl1080p, null, []),
            Score: 0,
            RejectionReasons: [],
            MatchedMusicVideoIds: []);

    [Fact]
    public async Task Successful_run_marks_item_completed_ready_to_import()
    {
        var (sut, _, _) = Build(progressLines:
        [
            "[youtube] abc: Downloading webpage",
            "[download]   0.0% of ~10.00MiB at 1.0MiB/s ETA 00:10",
            "[download]  50.0% of ~10.00MiB at 1.0MiB/s ETA 00:05",
            "[download] 100.0% of 10.00MiB",
        ]);

        var id = await sut.DownloadAsync(BuildRelease(), default);
        await sut.WaitForCompletionAsync(id);

        var items = await sut.GetItemsAsync(default);
        var item = items.Single(i => i.Id.Value == id.Value);
        item.Status.Should().Be(DownloadItemStatus.CompletedReadyToImport);
        item.OutputPath.Should().NotBeNullOrEmpty();
        item.RemainingBytes.Should().Be(0);
    }

    [Fact]
    public async Task Nonzero_exit_marks_failed_with_stderr_message()
    {
        var (sut, _, _) = Build(exitCode: 1, stderr: "ERROR: Video unavailable");

        var id = await sut.DownloadAsync(BuildRelease(), default);
        await sut.WaitForCompletionAsync(id);

        var item = (await sut.GetItemsAsync(default)).Single();
        item.Status.Should().Be(DownloadItemStatus.Failed);
        item.Message.Should().Contain("Video unavailable");
    }

    [Fact]
    public async Task Nonzero_exit_without_stderr_uses_default_message()
    {
        var (sut, _, _) = Build(exitCode: 1);

        var id = await sut.DownloadAsync(BuildRelease(), default);
        await sut.WaitForCompletionAsync(id);

        var item = (await sut.GetItemsAsync(default)).Single();
        item.Status.Should().Be(DownloadItemStatus.Failed);
        item.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Progress_line_at_50_percent_updates_remaining_bytes()
    {
        var record = new YtDlpDownload("x", "t", 10_000_000, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "[download]  50.0% of ~10.00MiB at 1.0MiB/s ETA 00:05");

        record.ProgressPercent.Should().Be(50);
        record.RemainingBytes.Should().NotBeNull().And.BeInRange(4_900_000, 5_100_000);
        record.Eta.Should().Be(TimeSpan.FromSeconds(5));
        record.Status.Should().Be(DownloadItemStatus.Downloading);
    }

    [Fact]
    public async Task Progress_line_with_unknown_eta_clears_eta()
    {
        var record = new YtDlpDownload("x", "t", 10_000_000, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "[download]  25.0% of ~10.00MiB at 1.0MiB/s ETA Unknown");
        record.Eta.Should().BeNull();
    }

    [Fact]
    public async Task Progress_line_infers_size_when_total_unknown()
    {
        var record = new YtDlpDownload("x", "t", null, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "[download]  20.0% of ~50.00MiB at 1.0MiB/s ETA 00:30");
        record.TotalBytes.Should().NotBeNull().And.BeInRange(52_300_000, 52_500_000);
    }

    [Fact]
    public async Task Progress_line_without_match_is_ignored()
    {
        var record = new YtDlpDownload("x", "t", 100, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "Some completely unrelated stderr noise");
        record.ProgressPercent.Should().Be(0);
        record.Status.Should().Be(DownloadItemStatus.Queued);
    }

    [Fact]
    public async Task Remove_with_delete_data_clears_output_files()
    {
        var (sut, _, fs) = Build(exitCode: 0);
        var id = await sut.DownloadAsync(BuildRelease(), default);
        await sut.WaitForCompletionAsync(id);

        var outputDir = Path.Combine("/tmp/incomplete", id.Value);
        fs.WriteFakeText(Path.Combine(outputDir, "video.mkv"), "fake");

        await sut.RemoveAsync(id, deleteData: true, default);

        (await sut.GetItemsAsync(default)).Should().BeEmpty();
        fs.FileExists(Path.Combine(outputDir, "video.mkv")).Should().BeFalse();
    }

    [Fact]
    public async Task Remove_without_delete_data_keeps_files()
    {
        var (sut, _, fs) = Build(exitCode: 0);
        var id = await sut.DownloadAsync(BuildRelease(), default);
        await sut.WaitForCompletionAsync(id);
        var outputDir = Path.Combine("/tmp/incomplete", id.Value);
        fs.WriteFakeText(Path.Combine(outputDir, "video.mkv"), "fake");

        await sut.RemoveAsync(id, deleteData: false, default);

        fs.FileExists(Path.Combine(outputDir, "video.mkv")).Should().BeTrue();
    }

    [Fact]
    public async Task Remove_unknown_id_is_no_op()
    {
        var (sut, _, _) = Build();
        await sut.RemoveAsync(new DownloadClientItemId("nope"), deleteData: true, default);
    }

    [Fact]
    public async Task Test_returns_version_when_yt_dlp_succeeds()
    {
        var (sut, _, _) = Build();
        var procs = new FakeProcessRunner().SetDefault(new ProcessResult(0, "2026.05.01", "", TimeSpan.Zero));
        var client = new YtDlpDownloadClient(1, "yt", new YtDlpDownloadClientSettings("/tmp"), procs, new FakeFileSystem());
        var result = await client.TestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("2026.05.01");
    }

    [Fact]
    public async Task Test_returns_failure_when_yt_dlp_missing()
    {
        var procs = new FakeProcessRunner().SetDefault(new ProcessResult(127, "", "command not found", TimeSpan.Zero));
        var client = new YtDlpDownloadClient(1, "yt", new YtDlpDownloadClientSettings("/tmp"), procs, new FakeFileSystem());
        var result = await client.TestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Be("command not found");
    }

    [Fact]
    public async Task Protocol_and_metadata_advertised()
    {
        var (sut, _, _) = Build();
        sut.Protocol.Should().Be(DownloadProtocol.Streaming);
        sut.Id.Should().Be(1);
        sut.Name.Should().Be("yt-dlp");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Wait_for_unknown_id_completes_immediately()
    {
        var (sut, _, _) = Build();
        await sut.WaitForCompletionAsync(new DownloadClientItemId("doesnt-exist"));
    }

    [Fact]
    public async Task Eta_parses_HHMMSS_format()
    {
        var record = new YtDlpDownload("x", "t", 100, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "[download]   1.0% of 100.00MiB at 1.0MiB/s ETA 01:02:03");
        record.Eta.Should().Be(new TimeSpan(1, 2, 3));
    }

    [Fact]
    public async Task Eta_parses_seconds_only_format()
    {
        var record = new YtDlpDownload("x", "t", 100, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "[download]   1.0% of 100.00MiB at 1.0MiB/s ETA 45");
        record.Eta.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task Eta_with_invalid_format_is_null()
    {
        var record = new YtDlpDownload("x", "t", 100, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "[download]   1.0% of 100.00MiB at 1.0MiB/s ETA xx:yy");
        record.Eta.Should().BeNull();
    }

    [Theory]
    [InlineData("[download]   1.0% of 200.50KiB", 200_000, 210_000)]
    [InlineData("[download]   1.0% of 1.50GiB", 1_500_000_000, 1_650_000_000)]
    [InlineData("[download]   1.0% of 1.00TiB", 1_000_000_000_000L, 1_200_000_000_000L)]
    public async Task Size_parser_accepts_kib_gib_tib_units(string line, long minBytes, long maxBytes)
    {
        var record = new YtDlpDownload("x", "t", null, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, line);
        record.TotalBytes.Should().NotBeNull().And.BeInRange(minBytes, maxBytes);
    }

    [Fact]
    public async Task Size_parser_handles_no_unit_as_bytes()
    {
        var record = new YtDlpDownload("x", "t", null, "/tmp/x");
        YtDlpDownloadClient.UpdateProgressFromLine(record, "[download]   1.0% of 1000");
        record.TotalBytes.Should().NotBeNull().And.BeInRange(900, 1100);
    }
}
