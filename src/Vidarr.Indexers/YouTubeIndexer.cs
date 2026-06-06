using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.Indexers;

public sealed class YouTubeIndexer : IIndexer
{
    private const string YtDlpExecutable = "yt-dlp";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IProcessRunner _processes;
    private readonly YouTubeIndexerSettings _settings;

    public YouTubeIndexer(int id, string name, YouTubeIndexerSettings settings, IProcessRunner processes)
    {
        Id = id;
        Name = name;
        _settings = settings;
        _processes = processes;
    }

    public int Id { get; }
    public string Name { get; }
    public DownloadProtocol Protocol => DownloadProtocol.Streaming;
    public bool SupportsRss => true;
    public bool SupportsSearch => true;

    public async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria criteria, CancellationToken ct)
    {
        var query = BuildSearchQuery(criteria);
        var args = new List<string>
        {
            "--no-warnings",
            "--ignore-config",
            "--flat-playlist",
            "--dump-json",
            "--playlist-end", _settings.MaxResults.ToString(CultureInfo.InvariantCulture),
            $"ytsearch{_settings.MaxResults}:{query}",
        };

        return await RunAndParseAsync(args, ct);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct)
    {
        if (_settings.ChannelIds.Count == 0)
        {
            return [];
        }

        var results = new List<ReleaseInfo>();
        foreach (var channelId in _settings.ChannelIds)
        {
            var url = $"https://www.youtube.com/channel/{channelId}/videos";
            var args = new List<string>
            {
                "--no-warnings",
                "--ignore-config",
                "--flat-playlist",
                "--dump-json",
                "--playlist-end", _settings.RssBatchSize.ToString(CultureInfo.InvariantCulture),
                url,
            };
            results.AddRange(await RunAndParseAsync(args, ct));
        }
        return results;
    }

    public async Task<IndexerTestResult> TestAsync(CancellationToken ct)
    {
        var invocation = new ProcessInvocation(YtDlpExecutable, ["--version"], Timeout: _settings.Timeout);
        var result = await _processes.RunAsync(invocation, ct);
        return result.ExitCode == 0
            ? new IndexerTestResult(true, $"yt-dlp {result.StdOut.Trim()}")
            : new IndexerTestResult(false, result.StdErr.Trim());
    }

    private static string BuildSearchQuery(IndexerSearchCriteria criteria)
    {
        if (!string.IsNullOrEmpty(criteria.ArtistName) && !string.IsNullOrEmpty(criteria.Title))
        {
            return $"{criteria.ArtistName} - {criteria.Title} official music video";
        }
        return string.IsNullOrEmpty(criteria.Query) ? "music video" : criteria.Query;
    }

    private async Task<IReadOnlyList<ReleaseInfo>> RunAndParseAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var invocation = new ProcessInvocation(YtDlpExecutable, args, Timeout: _settings.Timeout);
        var result = await _processes.RunAsync(invocation, ct);
        if (result.ExitCode != 0)
        {
            return [];
        }

        var releases = new List<ReleaseInfo>();
        foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            YtDlpEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<YtDlpEntry>(trimmed, JsonOpts);
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry is null || string.IsNullOrEmpty(entry.Id))
            {
                continue;
            }

            var url = entry.WebpageUrl ?? $"https://www.youtube.com/watch?v={entry.Id}";
            var publishedAt = entry.UploadDate is null
                ? (DateTimeOffset?)null
                : (DateTime.TryParseExact(entry.UploadDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? new DateTimeOffset(parsed, TimeSpan.Zero)
                    : null);

            var extras = new Dictionary<string, string>
            {
                ["youtubeId"] = entry.Id,
            };
            if (!string.IsNullOrEmpty(entry.Uploader))
            {
                extras["channelTitle"] = entry.Uploader;
            }
            if (!string.IsNullOrEmpty(entry.ChannelId))
            {
                extras["channelId"] = entry.ChannelId;
            }
            if (entry.Height is { } h)
            {
                extras["height"] = h.ToString(CultureInfo.InvariantCulture);
            }

            releases.Add(new ReleaseInfo(
                Title: entry.Title ?? string.Empty,
                SourceUrl: new Uri(url),
                Magnet: null,
                SizeBytes: entry.Filesize ?? entry.FilesizeApprox,
                PublishedAt: publishedAt,
                Age: publishedAt is { } at ? DateTimeOffset.UtcNow - at : null,
                Seeders: null,
                Leechers: null,
                Protocol: DownloadProtocol.Streaming,
                IndexerName: Name,
                IndexerCategory: "music-video",
                ExtraMetadata: extras));
        }
        return releases;
    }

    private sealed record YtDlpEntry(
        string? Id,
        string? Title,
        [property: JsonPropertyName("upload_date")] string? UploadDate,
        [property: JsonPropertyName("webpage_url")] string? WebpageUrl,
        string? Uploader,
        [property: JsonPropertyName("channel_id")] string? ChannelId,
        int? Height,
        long? Filesize,
        [property: JsonPropertyName("filesize_approx")] long? FilesizeApprox);
}

public sealed record YouTubeIndexerSettings(
    IReadOnlyList<string> ChannelIds,
    int MaxResults = 10,
    int RssBatchSize = 15,
    TimeSpan? Timeout = null)
{
    public static YouTubeIndexerSettings Default { get; } = new([]);
}
