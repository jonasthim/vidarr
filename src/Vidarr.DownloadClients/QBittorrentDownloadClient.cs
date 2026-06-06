using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.DownloadClients;

public sealed record QBittorrentSettings(
    Uri BaseUrl,
    string Username,
    string Password,
    string? Category = null,
    bool RemovesCompletedDownloads = false,
    TimeSpan? Timeout = null);

public sealed class QBittorrentDownloadClient : IDownloadClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClient _http;
    private readonly QBittorrentSettings _settings;
    private string? _sid;

    public QBittorrentDownloadClient(int id, string name, QBittorrentSettings settings, IHttpClient http)
    {
        Id = id;
        Name = name;
        _settings = settings;
        _http = http;
    }

    public int Id { get; }
    public string Name { get; }
    public DownloadProtocol Protocol => DownloadProtocol.Torrent;

    public async Task<DownloadClientItemId> DownloadAsync(RemoteRelease release, CancellationToken ct)
    {
        await EnsureAuthenticatedAsync(ct);

        var fields = new Dictionary<string, string>
        {
            ["urls"] = release.Info.Magnet ?? release.Info.SourceUrl.AbsoluteUri,
        };
        if (!string.IsNullOrEmpty(_settings.Category))
        {
            fields["category"] = _settings.Category;
        }
        var resp = await PostFormAsync("/api/v2/torrents/add", fields, ct);
        if (resp.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"qBittorrent add failed: HTTP {resp.StatusCode}");
        }

        // qBit's add endpoint doesn't return the hash directly. Use the source URL/magnet as
        // a stable client item id; GetItems matches by hash but the caller round-trips by id.
        var key = release.Info.Magnet ?? release.Info.SourceUrl.AbsoluteUri;
        return new DownloadClientItemId(key);
    }

    public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct)
    {
        await EnsureAuthenticatedAsync(ct);

        var path = string.IsNullOrEmpty(_settings.Category)
            ? "/api/v2/torrents/info"
            : $"/api/v2/torrents/info?category={Uri.EscapeDataString(_settings.Category)}";
        var resp = await GetAsync(path, ct);
        if (resp.StatusCode != 200 || string.IsNullOrEmpty(resp.Body))
        {
            return [];
        }

        var torrents = JsonSerializer.Deserialize<List<QBitTorrent>>(resp.Body, JsonOpts) ?? [];
        return [.. torrents.Select(MapItem)];
    }

    public async Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct)
    {
        await EnsureAuthenticatedAsync(ct);
        await PostFormAsync("/api/v2/torrents/delete", new Dictionary<string, string>
        {
            ["hashes"] = id.Value,
            ["deleteFiles"] = deleteData ? "true" : "false",
        }, ct);
    }

    public async Task<DownloadClientTestResult> TestAsync(CancellationToken ct)
    {
        try
        {
            await EnsureAuthenticatedAsync(ct);
            var resp = await GetAsync("/api/v2/app/version", ct);
            return resp.StatusCode is >= 200 and < 300
                ? new DownloadClientTestResult(true, $"qBittorrent {resp.Body.Trim()}")
                : new DownloadClientTestResult(false, $"HTTP {resp.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DownloadClientTestResult(false, ex.Message);
        }
    }

    public static DownloadClientItem MapItem(QBitTorrent t)
    {
        var status = MapStatus(t.State);
        return new DownloadClientItem(
            Id: new DownloadClientItemId(t.Hash ?? string.Empty),
            Title: t.Name ?? string.Empty,
            TotalBytes: t.Size,
            RemainingBytes: t.AmountLeft,
            Status: status,
            OutputPath: status is DownloadItemStatus.CompletedReadyToImport or DownloadItemStatus.Importing or DownloadItemStatus.Imported
                ? t.SavePath
                : null,
            Eta: t.Eta is null or 0 or 8_640_000 ? null : TimeSpan.FromSeconds(t.Eta.Value),
            Message: null);
    }

    public static DownloadItemStatus MapStatus(string? state) => state switch
    {
        "error" or "missingFiles" => DownloadItemStatus.Failed,
        "pausedDL" or "queuedDL" or "queuedUP" or "stalledUP" or "stalledDL" => DownloadItemStatus.Queued,
        "downloading" or "forcedDL" or "metaDL" or "allocating" or "checkingDL" or "checkingUP"
            or "checkingResumeData" or "moving" => DownloadItemStatus.Downloading,
        "uploading" or "forcedUP" or "pausedUP" => DownloadItemStatus.CompletedReadyToImport,
        _ => DownloadItemStatus.Queued,
    };

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_sid))
        {
            return;
        }

        var resp = await PostFormAsync("/api/v2/auth/login", new Dictionary<string, string>
        {
            ["username"] = _settings.Username,
            ["password"] = _settings.Password,
        }, ct, requireAuth: false);

        if (resp.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"qBittorrent login failed: HTTP {resp.StatusCode}");
        }

        // qBit responds with "Ok." on success and "Fails." on bad credentials. We trust the
        // SID-bearing Set-Cookie header; if the body is "Fails." we fail explicitly.
        if (string.Equals(resp.Body.Trim(), "Fails.", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("qBittorrent login rejected credentials");
        }

        _sid = ExtractSidCookie(resp.Headers);
        if (string.IsNullOrEmpty(_sid))
        {
            throw new InvalidOperationException("qBittorrent login succeeded but no SID cookie returned");
        }
    }

    public static string? ExtractSidCookie(IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Set-Cookie", out var setCookie))
        {
            return null;
        }
        // Set-Cookie may concatenate multiple cookies separated by ",". Walk segments looking for SID.
        foreach (var part in setCookie.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("SID=", StringComparison.OrdinalIgnoreCase))
            {
                return part["SID=".Length..];
            }
        }
        return null;
    }

    private Task<HttpClientResponse> GetAsync(string path, CancellationToken ct) =>
        _http.SendAsync(new HttpClientRequest(
            HttpMethod.Get,
            new Uri(BaseAuthority(), path),
            BuildHeaders(),
            Timeout: _settings.Timeout), ct);

    private Task<HttpClientResponse> PostFormAsync(string path, IReadOnlyDictionary<string, string> fields, CancellationToken ct, bool requireAuth = true)
    {
        var sb = new StringBuilder();
        foreach (var (k, v) in fields)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v));
        }
        var headers = BuildHeaders();
        headers["Content-Type"] = "application/x-www-form-urlencoded";
        if (!requireAuth)
        {
            // Login itself doesn't need a SID. Drop the cookie if present.
            headers.Remove("Cookie");
        }
        return _http.SendAsync(new HttpClientRequest(
            HttpMethod.Post,
            new Uri(BaseAuthority(), path),
            headers,
            new HttpClientContent.Bytes("application/x-www-form-urlencoded", Encoding.UTF8.GetBytes(sb.ToString())),
            _settings.Timeout), ct);
    }

    private Dictionary<string, string> BuildHeaders()
    {
        var h = new Dictionary<string, string>
        {
            ["User-Agent"] = "Vidarr/1.0",
            ["Referer"] = _settings.BaseUrl.AbsoluteUri,
        };
        if (!string.IsNullOrEmpty(_sid))
        {
            h["Cookie"] = $"SID={_sid}";
        }
        return h;
    }

    private Uri BaseAuthority() => new(_settings.BaseUrl, "/");

    public sealed class QBitTorrent
    {
        public string? Hash { get; set; }
        public string? Name { get; set; }
        public long Size { get; set; }
        [JsonPropertyName("amount_left")] public long? AmountLeft { get; set; }
        public string? State { get; set; }
        [JsonPropertyName("save_path")] public string? SavePath { get; set; }
        public long? Eta { get; set; }
        public double? Progress { get; set; }
    }
}
