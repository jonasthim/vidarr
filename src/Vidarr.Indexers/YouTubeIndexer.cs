using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using System.Xml.Linq;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.Indexers;

public sealed class YouTubeIndexer : IIndexer
{
    private const string YtDlpExecutable = "yt-dlp";
    private static readonly Uri YouTubeRssBase = new("https://www.youtube.com/feeds/videos.xml");
    private static readonly Uri YouTubeDataApiBase = new("https://www.googleapis.com/youtube/v3/");
    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace YtNs = "http://www.youtube.com/xml/schemas/2015";
    private static readonly XNamespace MediaNs = "http://search.yahoo.com/mrss/";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IProcessRunner _processes;
    private readonly IHttpClient _http;
    private readonly IYouTubeQualityMapper _qualityMapper;
    private readonly YouTubeIndexerSettings _settings;
    private bool _dataApiQuotaExceeded;

    public YouTubeIndexer(
        int id,
        string name,
        YouTubeIndexerSettings settings,
        IProcessRunner processes,
        IHttpClient http,
        IYouTubeQualityMapper qualityMapper)
    {
        Id = id;
        Name = name;
        _settings = settings;
        _processes = processes;
        _http = http;
        _qualityMapper = qualityMapper;
    }

    public int Id { get; }
    public string Name { get; }
    public DownloadProtocol Protocol => DownloadProtocol.Streaming;
    public bool SupportsRss => true;
    public bool SupportsSearch => true;

    /// <summary>True after the Data API has returned 403 + quotaExceeded; the indexer
    /// will skip the API path and only use yt-dlp scraping until the next process restart.</summary>
    public bool DataApiQuotaExceeded => _dataApiQuotaExceeded;

    public async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria criteria, CancellationToken ct)
    {
        var query = BuildSearchQuery(criteria);

        if (!string.IsNullOrEmpty(_settings.DataApiKey) && !_dataApiQuotaExceeded)
        {
            var apiResult = await TryDataApiSearchAsync(query, ct);
            if (apiResult is not null)
            {
                return apiResult;
            }
        }

        return await YtDlpSearchAsync(query, ct);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct)
    {
        if (_settings.ChannelIds.Count == 0)
        {
            return [];
        }

        var releases = new List<ReleaseInfo>();
        foreach (var channelId in _settings.ChannelIds)
        {
            releases.AddRange(await FetchChannelRssAsync(channelId, ct));
        }
        return releases;
    }

    public async Task<IndexerTestResult> TestAsync(CancellationToken ct)
    {
        // When a Data API key is configured, validate it; otherwise fall back to a
        // yt-dlp version check.
        if (!string.IsNullOrEmpty(_settings.DataApiKey))
        {
            var uri = BuildDataApiUri("search", new Dictionary<string, string>
            {
                ["part"] = "snippet",
                ["q"] = "vidarr",
                ["type"] = "video",
                ["maxResults"] = "1",
                ["key"] = _settings.DataApiKey,
            });
            var resp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Get, uri, BuildAcceptJsonHeaders(), Timeout: _settings.Timeout), ct);
            return resp.StatusCode is >= 200 and < 300
                ? new IndexerTestResult(true, "YouTube Data API key OK")
                : new IndexerTestResult(false, $"Data API HTTP {resp.StatusCode}");
        }

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

    private async Task<List<ReleaseInfo>?> TryDataApiSearchAsync(string query, CancellationToken ct)
    {
        var searchUri = BuildDataApiUri("search", new Dictionary<string, string>
        {
            ["part"] = "snippet",
            ["type"] = "video",
            ["maxResults"] = _settings.MaxResults.ToString(CultureInfo.InvariantCulture),
            ["q"] = query,
            ["key"] = _settings.DataApiKey ?? string.Empty,
        });
        var searchResp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Get, searchUri, BuildAcceptJsonHeaders(), Timeout: _settings.Timeout), ct);
        if (IsQuotaExceeded(searchResp))
        {
            _dataApiQuotaExceeded = true;
            return null;
        }
        if (searchResp.StatusCode != 200 || string.IsNullOrEmpty(searchResp.Body))
        {
            return null;
        }

        var searchDoc = JsonSerializer.Deserialize<YtSearchListResponse>(searchResp.Body, JsonOpts);
        if (searchDoc?.Items is null or { Count: 0 })
        {
            return [];
        }

        var releases = searchDoc.Items
            .Where(i => i.Id?.VideoId is not null)
            .Select(i => BuildReleaseInfo(
                videoId: i.Id!.VideoId!,
                title: i.Snippet?.Title ?? string.Empty,
                channelTitle: i.Snippet?.ChannelTitle,
                channelId: i.Snippet?.ChannelId,
                publishedRaw: i.Snippet?.PublishedAt,
                height: null,
                sizeBytes: null))
            .ToList();

        // Bonus enrichment: videos.list gives us contentDetails.duration + (when present)
        // contentDetails.definition (sd/hd) which the quality mapper folds in to a height
        // approximation. Only enrich if the caller has a valid key and we haven't hit quota.
        var videoIds = releases
            .Select(r => r.ExtraMetadata.GetValueOrDefault("youtubeId"))
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .Take(50) // Data API caps videos.list at 50 ids per call.
            .ToList();
        if (videoIds.Count > 0)
        {
            var videosUri = BuildDataApiUri("videos", new Dictionary<string, string>
            {
                ["part"] = "contentDetails",
                ["id"] = string.Join(',', videoIds!),
                ["key"] = _settings.DataApiKey ?? string.Empty,
            });
            var videosResp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Get, videosUri, BuildAcceptJsonHeaders(), Timeout: _settings.Timeout), ct);
            if (IsQuotaExceeded(videosResp))
            {
                _dataApiQuotaExceeded = true;
            }
            else if (videosResp.StatusCode == 200)
            {
                YtVideosListResponse? videosDoc = null;
                try
                {
                    videosDoc = JsonSerializer.Deserialize<YtVideosListResponse>(videosResp.Body, JsonOpts);
                }
                catch (JsonException)
                {
                    // Tolerate a mismatched body — Test/URL-shape tests don't seed the
                    // /videos response and the SearchListResponse shape is incompatible.
                }

                if (videosDoc?.Items is not null)
                {
                    var byId = videosDoc.Items.ToDictionary(v => v.Id ?? string.Empty, v => v.ContentDetails);
                    for (var i = 0; i < releases.Count; i++)
                    {
                        var youtubeId = releases[i].ExtraMetadata.GetValueOrDefault("youtubeId");
                        if (youtubeId is null) continue;
                        if (!byId.TryGetValue(youtubeId, out var details) || details is null) continue;

                        // hd = 720p+, sd = best-effort 480p
                        var height = string.Equals(details.Definition, "hd", StringComparison.OrdinalIgnoreCase) ? 720 : 480;
                        var enriched = (Dictionary<string, string>)releases[i].ExtraMetadata;
                        enriched["height"] = height.ToString(CultureInfo.InvariantCulture);
                        var newQuality = _qualityMapper.FromHeight(height);
                        if (newQuality != Quality.Unknown)
                        {
                            enriched["quality"] = newQuality.Name;
                        }
                        if (!string.IsNullOrEmpty(details.Duration))
                        {
                            enriched["duration"] = details.Duration;
                        }
                    }
                }
            }
        }

        return releases;
    }

    private async Task<List<ReleaseInfo>> YtDlpSearchAsync(string query, CancellationToken ct)
    {
        var args = new List<string>
        {
            "--no-warnings",
            "--ignore-config",
            "--flat-playlist",
            "--dump-json",
            "--playlist-end", _settings.MaxResults.ToString(CultureInfo.InvariantCulture),
            $"ytsearch{_settings.MaxResults}:{query}",
        };
        return await RunYtDlpAndParseAsync(args, ct);
    }

    private async Task<List<ReleaseInfo>> FetchChannelRssAsync(string channelId, CancellationToken ct)
    {
        var uri = new Uri($"{YouTubeRssBase.AbsoluteUri}?channel_id={Uri.EscapeDataString(channelId)}");
        var resp = await _http.SendAsync(new HttpClientRequest(
            HttpMethod.Get, uri,
            new Dictionary<string, string> { ["User-Agent"] = "Vidarr/1.0", ["Accept"] = "application/atom+xml" },
            Timeout: _settings.Timeout), ct);
        if (resp.StatusCode != 200 || string.IsNullOrEmpty(resp.Body))
        {
            return [];
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(resp.Body);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var releases = new List<ReleaseInfo>();
        foreach (var entry in doc.Descendants(AtomNs + "entry").Take(_settings.RssBatchSize))
        {
            var videoId = entry.Element(YtNs + "videoId")?.Value;
            if (string.IsNullOrEmpty(videoId)) continue;
            var title = entry.Element(AtomNs + "title")?.Value ?? string.Empty;
            var publishedRaw = entry.Element(AtomNs + "published")?.Value;
            var channelTitle = entry.Element(AtomNs + "author")?.Element(AtomNs + "name")?.Value;
            int? height = null;
            var mediaContent = entry.Element(MediaNs + "group")?.Element(MediaNs + "content");
            if (mediaContent is not null && int.TryParse(mediaContent.Attribute("height")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
            {
                height = h;
            }

            releases.Add(BuildReleaseInfo(
                videoId: videoId,
                title: title,
                channelTitle: channelTitle,
                channelId: channelId,
                publishedRaw: publishedRaw,
                height: height,
                sizeBytes: null));
        }
        return releases;
    }

    private async Task<List<ReleaseInfo>> RunYtDlpAndParseAsync(IReadOnlyList<string> args, CancellationToken ct)
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
            if (string.IsNullOrEmpty(trimmed)) continue;

            YtDlpEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<YtDlpEntry>(trimmed, JsonOpts);
            }
            catch (JsonException)
            {
                continue;
            }
            if (entry is null || string.IsNullOrEmpty(entry.Id)) continue;

            releases.Add(BuildReleaseInfo(
                videoId: entry.Id,
                title: entry.Title ?? string.Empty,
                channelTitle: entry.Uploader,
                channelId: entry.ChannelId,
                publishedRaw: entry.UploadDate,
                height: entry.Height,
                sizeBytes: entry.Filesize ?? entry.FilesizeApprox,
                webpageUrl: entry.WebpageUrl));
        }
        return releases;
    }

    private ReleaseInfo BuildReleaseInfo(
        string videoId,
        string title,
        string? channelTitle,
        string? channelId,
        string? publishedRaw,
        int? height,
        long? sizeBytes,
        string? webpageUrl = null)
    {
        var url = webpageUrl ?? $"https://www.youtube.com/watch?v={videoId}";
        var publishedAt = ParsePublished(publishedRaw);
        var quality = _qualityMapper.FromHeight(height);

        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["youtubeId"] = videoId,
        };
        if (!string.IsNullOrEmpty(channelTitle)) extras["channelTitle"] = channelTitle;
        if (!string.IsNullOrEmpty(channelId)) extras["channelId"] = channelId;
        if (height is { } h) extras["height"] = h.ToString(CultureInfo.InvariantCulture);
        if (quality != Quality.Unknown) extras["quality"] = quality.Name;

        return new ReleaseInfo(
            Title: title,
            SourceUrl: new Uri(url),
            Magnet: null,
            SizeBytes: sizeBytes,
            PublishedAt: publishedAt,
            Age: publishedAt is { } at ? DateTimeOffset.UtcNow - at : null,
            Seeders: null,
            Leechers: null,
            Protocol: DownloadProtocol.Streaming,
            IndexerName: Name,
            IndexerCategory: "music-video",
            ExtraMetadata: extras);
    }

    private static DateTimeOffset? ParsePublished(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        // yt-dlp emits yyyyMMdd; Data API + RSS emit ISO-8601.
        if (DateTime.TryParseExact(raw, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return new DateTimeOffset(dt, TimeSpan.Zero);
        }
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var iso))
        {
            return iso.ToUniversalTime();
        }
        return null;
    }

    private static Uri BuildDataApiUri(string path, IReadOnlyDictionary<string, string> query)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        foreach (var (k, v) in query)
        {
            qs[k] = v;
        }
        return new Uri(YouTubeDataApiBase, $"{path}?{qs}");
    }

    private static Dictionary<string, string> BuildAcceptJsonHeaders() => new()
    {
        ["User-Agent"] = "Vidarr/1.0",
        ["Accept"] = "application/json",
    };

    public static bool IsQuotaExceeded(HttpClientResponse resp)
    {
        if (resp.StatusCode != 403)
        {
            return false;
        }
        if (string.IsNullOrEmpty(resp.Body))
        {
            return true;
        }
        return resp.Body.Contains("quotaExceeded", StringComparison.OrdinalIgnoreCase)
            || resp.Body.Contains("dailyLimitExceeded", StringComparison.OrdinalIgnoreCase);
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

    private sealed record YtSearchListResponse(List<YtSearchItem>? Items);
    private sealed record YtSearchItem(YtSearchItemId? Id, YtSnippet? Snippet);
    private sealed record YtSearchItemId([property: JsonPropertyName("videoId")] string? VideoId);
    private sealed record YtSnippet(string? Title, [property: JsonPropertyName("channelTitle")] string? ChannelTitle, [property: JsonPropertyName("channelId")] string? ChannelId, [property: JsonPropertyName("publishedAt")] string? PublishedAt);
    private sealed record YtVideosListResponse(List<YtVideoItem>? Items);
    private sealed record YtVideoItem(string? Id, [property: JsonPropertyName("contentDetails")] YtVideoContentDetails? ContentDetails);
    private sealed record YtVideoContentDetails(string? Duration, string? Definition);
}

public sealed record YouTubeIndexerSettings(
    IReadOnlyList<string> ChannelIds,
    int MaxResults = 10,
    int RssBatchSize = 15,
    string? DataApiKey = null,
    TimeSpan? Timeout = null)
{
    public static YouTubeIndexerSettings Default { get; } = new([]);
}
