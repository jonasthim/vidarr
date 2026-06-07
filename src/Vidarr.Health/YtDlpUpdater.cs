using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Health;

public sealed record YtDlpUpdateResult(
    bool Updated,
    string? InstalledVersion,
    string? LatestVersion,
    string? Reason);

public sealed record YtDlpUpdaterOptions(
    string BinaryPath,
    string ReleaseFeedUrl = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest",
    string AssetName = "yt-dlp");

/// <summary>
/// Tiny boundary over binary HTTP downloads, separate from IHttpClient (which is
/// string-bodied). Lets the updater fetch the yt-dlp asset and lets tests inject
/// a deterministic byte payload.
/// </summary>
public interface IBinaryDownloader
{
    Task<byte[]> DownloadAsync(Uri url, IReadOnlyDictionary<string, string>? headers, CancellationToken ct);
}

public interface IYtDlpUpdater
{
    Task<YtDlpUpdateResult> CheckAndUpdateAsync(CancellationToken ct);
}

public sealed class YtDlpUpdater : IYtDlpUpdater
{
    private static readonly Dictionary<string, string> GithubHeaders = new()
    {
        ["User-Agent"] = "vidarr",
        ["Accept"] = "application/vnd.github+json",
    };

    private readonly YtDlpUpdaterOptions _options;
    private readonly IProcessRunner _processes;
    private readonly IHttpClient _http;
    private readonly IBinaryDownloader _downloader;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<YtDlpUpdater> _logger;

    public YtDlpUpdater(
        YtDlpUpdaterOptions options,
        IProcessRunner processes,
        IHttpClient http,
        IBinaryDownloader downloader,
        IFileSystem fileSystem,
        ILogger<YtDlpUpdater> logger)
    {
        _options = options;
        _processes = processes;
        _http = http;
        _downloader = downloader;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<YtDlpUpdateResult> CheckAndUpdateAsync(CancellationToken ct)
    {
        var installed = await ReadInstalledVersionAsync(ct);
        var latest = await ReadLatestReleaseAsync(ct);
        if (latest is null)
        {
            return new YtDlpUpdateResult(false, installed, null, "Could not query release feed");
        }
        if (!string.IsNullOrEmpty(installed)
            && string.Equals(installed, latest.TagName, StringComparison.OrdinalIgnoreCase))
        {
            return new YtDlpUpdateResult(false, installed, latest.TagName, "Already up to date");
        }

        var assetUrl = latest.Assets
            .FirstOrDefault(a => string.Equals(a.Name, _options.AssetName, StringComparison.OrdinalIgnoreCase))?.DownloadUrl;
        if (assetUrl is null)
        {
            return new YtDlpUpdateResult(false, installed, latest.TagName,
                $"Asset {_options.AssetName} not found in latest release");
        }

        byte[] bytes;
        try
        {
            bytes = await _downloader.DownloadAsync(new Uri(assetUrl), GithubHeaders, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to download {Url}", assetUrl);
            return new YtDlpUpdateResult(false, installed, latest.TagName, $"Download failed: {ex.Message}");
        }
        if (bytes.Length == 0)
        {
            return new YtDlpUpdateResult(false, installed, latest.TagName, "Empty payload from asset URL");
        }

        var stagedPath = _options.BinaryPath + ".new";
        await _fileSystem.WriteAllBytesAsync(stagedPath, bytes, ct);
        await MakeExecutableAsync(stagedPath, ct);
        _fileSystem.MoveFile(stagedPath, _options.BinaryPath, overwrite: true);
        _logger.LogInformation("Updated yt-dlp {From} → {To} at {Path}",
            installed ?? "(unknown)", latest.TagName, _options.BinaryPath);
        return new YtDlpUpdateResult(true, installed, latest.TagName, null);
    }

    private async Task<string?> ReadInstalledVersionAsync(CancellationToken ct)
    {
        try
        {
            var result = await _processes.RunAsync(
                new ProcessInvocation(_options.BinaryPath, ["--version"], Timeout: TimeSpan.FromSeconds(10)),
                ct);
            return result.ExitCode == 0 ? result.StdOut.Trim() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not invoke {Path} --version", _options.BinaryPath);
            return null;
        }
    }

    private async Task<GitHubRelease?> ReadLatestReleaseAsync(CancellationToken ct)
    {
        try
        {
            var resp = await _http.SendAsync(new HttpClientRequest(
                Method: HttpMethod.Get,
                Uri: new Uri(_options.ReleaseFeedUrl),
                Headers: GithubHeaders), ct);
            if (resp.StatusCode is < 200 or >= 300)
            {
                _logger.LogWarning("Release feed returned HTTP {Code}", resp.StatusCode);
                return null;
            }
            return JsonSerializer.Deserialize<GitHubRelease>(resp.Body, JsonOpts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to read latest release from {Url}", _options.ReleaseFeedUrl);
            return null;
        }
    }

    private async Task MakeExecutableAsync(string path, CancellationToken ct)
    {
        try
        {
            await _processes.RunAsync(
                new ProcessInvocation("chmod", ["+x", path], Timeout: TimeSpan.FromSeconds(5)),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "chmod +x failed for {Path}", path);
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private sealed record GitHubRelease(
        [property: System.Text.Json.Serialization.JsonPropertyName("tag_name")] string TagName,
        IReadOnlyList<GitHubAsset> Assets);
    private sealed record GitHubAsset(string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("browser_download_url")] string DownloadUrl);
}
