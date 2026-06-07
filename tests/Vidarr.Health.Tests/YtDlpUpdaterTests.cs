using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Contracts.Abstractions;
using Vidarr.Health;
using Vidarr.Tests.Common;

namespace Vidarr.Health.Tests;

public class YtDlpUpdaterTests
{
    private const string ReleaseUrl = "https://example.invalid/releases/latest";
    private const string BinaryPath = "/bin/yt-dlp";

    private static string ReleaseJson(string tag, string assetName, string downloadUrl) =>
        $"{{\"tag_name\":\"{tag}\",\"assets\":[{{\"name\":\"{assetName}\",\"browser_download_url\":\"{downloadUrl}\"}}]}}";

    private static YtDlpUpdater NewUpdater(
        FakeProcessRunner procs, FakeHttpClient http, RecordingBinaryDownloader dl, FakeFileSystem fs) =>
        new(new YtDlpUpdaterOptions(BinaryPath, ReleaseUrl), procs, http, dl, fs, NullLogger<YtDlpUpdater>.Instance);

    [Fact]
    public async Task Reports_already_up_to_date_when_versions_match()
    {
        var procs = new FakeProcessRunner();
        var http = new FakeHttpClient();
        var dl = new RecordingBinaryDownloader();
        var fs = new FakeFileSystem();
        procs.WhenExecutable(BinaryPath, new ProcessResult(0, "2026.05.01", string.Empty, TimeSpan.Zero));
        http.WhenRequest(_ => true, HttpClientResponseFactory.Json(
            ReleaseJson("2026.05.01", "yt-dlp", "https://example.invalid/asset")));

        var result = await NewUpdater(procs, http, dl, fs).CheckAndUpdateAsync(default);

        result.Updated.Should().BeFalse();
        result.Reason.Should().Contain("Already up to date");
        dl.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Downloads_and_swaps_when_newer_version_available()
    {
        var procs = new FakeProcessRunner();
        var http = new FakeHttpClient();
        var fs = new FakeFileSystem();
        var dl = new RecordingBinaryDownloader([0x7f, 0x45, 0x4c, 0x46]);
        procs.WhenExecutable(BinaryPath, new ProcessResult(0, "2026.05.01", string.Empty, TimeSpan.Zero));
        procs.WhenExecutable("chmod", new ProcessResult(0, string.Empty, string.Empty, TimeSpan.Zero));
        http.WhenRequest(_ => true, HttpClientResponseFactory.Json(
            ReleaseJson("2026.06.01", "yt-dlp", "https://example.invalid/asset")));

        var result = await NewUpdater(procs, http, dl, fs).CheckAndUpdateAsync(default);

        result.Updated.Should().BeTrue();
        result.LatestVersion.Should().Be("2026.06.01");
        result.InstalledVersion.Should().Be("2026.05.01");
        fs.Files.Should().ContainKey(BinaryPath);
        fs.Files[BinaryPath].Should().BeEquivalentTo(new byte[] { 0x7f, 0x45, 0x4c, 0x46 });
        dl.Calls.Should().ContainSingle();
        procs.Invocations.Should().Contain(i => i.Executable == "chmod");
    }

    [Fact]
    public async Task Reports_failure_when_release_feed_unreachable()
    {
        var procs = new FakeProcessRunner();
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(503, new Dictionary<string, string>(), string.Empty));
        var dl = new RecordingBinaryDownloader();
        var fs = new FakeFileSystem();
        var result = await NewUpdater(procs, http, dl, fs).CheckAndUpdateAsync(default);
        result.Updated.Should().BeFalse();
        result.Reason.Should().Contain("Could not query");
    }

    [Fact]
    public async Task Reports_failure_when_asset_missing()
    {
        var procs = new FakeProcessRunner();
        var http = new FakeHttpClient();
        var dl = new RecordingBinaryDownloader();
        var fs = new FakeFileSystem();
        procs.WhenExecutable(BinaryPath, new ProcessResult(0, "old", string.Empty, TimeSpan.Zero));
        http.WhenRequest(_ => true, HttpClientResponseFactory.Json(
            ReleaseJson("new", "wrong-asset", "https://example.invalid/asset")));
        var result = await NewUpdater(procs, http, dl, fs).CheckAndUpdateAsync(default);
        result.Updated.Should().BeFalse();
        result.Reason.Should().Contain("not found");
    }

    [Fact]
    public async Task Reports_failure_when_download_throws()
    {
        var procs = new FakeProcessRunner();
        var http = new FakeHttpClient();
        var fs = new FakeFileSystem();
        var dl = new RecordingBinaryDownloader(new InvalidOperationException("nope"));
        procs.WhenExecutable(BinaryPath, new ProcessResult(0, "old", string.Empty, TimeSpan.Zero));
        http.WhenRequest(_ => true, HttpClientResponseFactory.Json(
            ReleaseJson("new", "yt-dlp", "https://example.invalid/asset")));
        var result = await NewUpdater(procs, http, dl, fs).CheckAndUpdateAsync(default);
        result.Updated.Should().BeFalse();
        result.Reason.Should().Contain("nope");
    }

    [Fact]
    public async Task Reports_failure_on_empty_payload()
    {
        var procs = new FakeProcessRunner();
        var http = new FakeHttpClient();
        var fs = new FakeFileSystem();
        var dl = new RecordingBinaryDownloader([]);
        procs.WhenExecutable(BinaryPath, new ProcessResult(0, "old", string.Empty, TimeSpan.Zero));
        http.WhenRequest(_ => true, HttpClientResponseFactory.Json(
            ReleaseJson("new", "yt-dlp", "https://example.invalid/asset")));
        var result = await NewUpdater(procs, http, dl, fs).CheckAndUpdateAsync(default);
        result.Updated.Should().BeFalse();
        result.Reason.Should().Contain("Empty payload");
    }

    [Fact]
    public async Task Proceeds_with_unknown_installed_version_when_yt_dlp_missing()
    {
        var procs = new FakeProcessRunner();
        var http = new FakeHttpClient();
        var fs = new FakeFileSystem();
        var dl = new RecordingBinaryDownloader([0x01]);
        // No matcher for binary path → default with non-zero exit means InstalledVersion stays null.
        procs.SetDefault(new ProcessResult(127, string.Empty, "not found", TimeSpan.Zero));
        http.WhenRequest(_ => true, HttpClientResponseFactory.Json(
            ReleaseJson("first-install", "yt-dlp", "https://example.invalid/asset")));
        var result = await NewUpdater(procs, http, dl, fs).CheckAndUpdateAsync(default);
        result.Updated.Should().BeTrue();
        result.InstalledVersion.Should().BeNull();
    }

    private sealed class RecordingBinaryDownloader : IBinaryDownloader
    {
        private readonly byte[]? _payload;
        private readonly Exception? _throws;
        public RecordingBinaryDownloader() { }
        public RecordingBinaryDownloader(byte[] payload) { _payload = payload; }
        public RecordingBinaryDownloader(Exception throws) { _throws = throws; }
        public List<Uri> Calls { get; } = [];
        public Task<byte[]> DownloadAsync(Uri url, IReadOnlyDictionary<string, string>? headers, CancellationToken ct)
        {
            Calls.Add(url);
            if (_throws is not null) return Task.FromException<byte[]>(_throws);
            return Task.FromResult(_payload ?? []);
        }
    }
}
