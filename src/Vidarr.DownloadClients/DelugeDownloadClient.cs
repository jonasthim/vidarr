using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.DownloadClients;

public sealed record DelugeSettings(
    Uri BaseUrl,
    string Password,
    string? Category = null,
    string? DownloadLocation = null,
    TimeSpan? Timeout = null);

/// <summary>
/// Deluge's WebUI exposes JSON-RPC 2.0 at <c>/json</c>. Auth: POST {method:auth.login,
/// params:[password]} returns <c>_session_id</c> cookie that subsequent requests reuse.
/// </summary>
public sealed class DelugeDownloadClient : IDownloadClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClient _http;
    private readonly DelugeSettings _settings;
    private string? _sessionCookie;
    private int _rpcId;

    public DelugeDownloadClient(int id, string name, DelugeSettings settings, IHttpClient http)
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

        var url = release.Info.Magnet ?? release.Info.SourceUrl.AbsoluteUri;
        var method = release.Info.Magnet is not null ? "core.add_torrent_magnet" : "core.add_torrent_url";

        var options = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(_settings.DownloadLocation))
        {
            options["download_location"] = _settings.DownloadLocation;
        }

        var response = await CallAsync(method, [url, options], ct);
        // Deluge returns { result: <hash> | null, error: null }
        if (response.Error is not null)
        {
            throw new InvalidOperationException($"Deluge add failed: {response.Error.Message}");
        }
        var hash = response.Result?.GetString()
            ?? throw new InvalidOperationException("Deluge returned no torrent hash");
        if (!string.IsNullOrEmpty(_settings.Category))
        {
            await CallAsync("label.set_torrent", [hash, _settings.Category], ct);
        }
        return new DownloadClientItemId(hash);
    }

    public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct)
    {
        await EnsureAuthenticatedAsync(ct);

        var filter = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(_settings.Category))
        {
            filter["label"] = _settings.Category;
        }
        var fields = new[]
        {
            "hash", "name", "total_size", "state", "progress",
            "eta", "save_path", "error_code",
        };
        var response = await CallAsync("core.get_torrents_status", [filter, fields], ct);
        if (response.Error is not null || response.Result is null)
        {
            return [];
        }

        if (response.Result.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var items = new List<DownloadClientItem>();
        foreach (var prop in response.Result.Value.EnumerateObject())
        {
            var torrent = JsonSerializer.Deserialize<DelugeTorrent>(prop.Value.GetRawText(), JsonOpts);
            if (torrent is not null)
            {
                if (string.IsNullOrEmpty(torrent.Hash))
                {
                    torrent.Hash = prop.Name;
                }
                items.Add(MapItem(torrent));
            }
        }
        return items;
    }

    public async Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct)
    {
        await EnsureAuthenticatedAsync(ct);
        await CallAsync("core.remove_torrent", [id.Value, deleteData], ct);
    }

    public async Task<DownloadClientTestResult> TestAsync(CancellationToken ct)
    {
        try
        {
            var login = await AttemptLoginAsync(ct);
            if (!login)
            {
                return new DownloadClientTestResult(false, "login rejected credentials");
            }
            var info = await CallAsync("daemon.info", [], ct);
            if (info.Error is not null)
            {
                return new DownloadClientTestResult(false, info.Error.Message);
            }
            return new DownloadClientTestResult(true, $"Deluge {info.Result?.GetString() ?? "OK"}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DownloadClientTestResult(false, ex.Message);
        }
    }

    public static DownloadClientItem MapItem(DelugeTorrent t)
    {
        var status = MapStatus(t.State);
        return new DownloadClientItem(
            Id: new DownloadClientItemId(t.Hash ?? string.Empty),
            Title: t.Name ?? string.Empty,
            TotalBytes: t.TotalSize,
            RemainingBytes: t.TotalSize is { } total && t.Progress is { } pr
                ? (long?)Math.Max(0, total * (1 - pr / 100.0))
                : null,
            Status: status,
            OutputPath: status is DownloadItemStatus.CompletedReadyToImport or DownloadItemStatus.Imported
                ? t.SavePath
                : null,
            Eta: t.Eta is > 0 ? TimeSpan.FromSeconds(t.Eta.Value) : null,
            Message: string.IsNullOrEmpty(t.ErrorCode) ? null : t.ErrorCode);
    }

    public static DownloadItemStatus MapStatus(string? state) => state switch
    {
        "Error" => DownloadItemStatus.Failed,
        "Queued" or "Paused" => DownloadItemStatus.Queued,
        "Downloading" or "Checking" or "Allocating" => DownloadItemStatus.Downloading,
        "Seeding" or "Active" => DownloadItemStatus.CompletedReadyToImport,
        _ => DownloadItemStatus.Queued,
    };

    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_sessionCookie))
        {
            return;
        }
        var ok = await AttemptLoginAsync(ct);
        if (!ok)
        {
            throw new InvalidOperationException("Deluge auth.login returned false");
        }
    }

    private async Task<bool> AttemptLoginAsync(CancellationToken ct)
    {
        var resp = await CallAsync("auth.login", [_settings.Password], ct, skipAuthEnsure: true);
        if (resp.Error is not null)
        {
            return false;
        }
        var success = resp.Result?.ValueKind == JsonValueKind.True;
        if (!success)
        {
            return false;
        }
        return true;
    }

    private async Task<DelugeRpcResponse> CallAsync(string method, object[] @params, CancellationToken ct, bool skipAuthEnsure = false)
    {
        if (!skipAuthEnsure && string.IsNullOrEmpty(_sessionCookie))
        {
            await EnsureAuthenticatedAsync(ct);
        }

        var payload = JsonSerializer.Serialize(new
        {
            method,
            @params,
            id = Interlocked.Increment(ref _rpcId),
        });

        var headers = new Dictionary<string, string>
        {
            ["User-Agent"] = "Vidarr/1.0",
        };
        if (!string.IsNullOrEmpty(_sessionCookie))
        {
            headers["Cookie"] = _sessionCookie;
        }

        var http = await _http.SendAsync(new HttpClientRequest(
            HttpMethod.Post,
            new Uri(_settings.BaseUrl, "/json"),
            headers,
            new HttpClientContent.Json(payload),
            _settings.Timeout), ct);

        if (http.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"Deluge RPC HTTP {http.StatusCode}");
        }

        if (string.IsNullOrEmpty(_sessionCookie)
            && http.Headers.TryGetValue("Set-Cookie", out var setCookie))
        {
            var sessionId = ExtractSessionCookie(setCookie);
            if (!string.IsNullOrEmpty(sessionId))
            {
                _sessionCookie = $"_session_id={sessionId}";
            }
        }

        var parsed = JsonSerializer.Deserialize<DelugeRpcResponse>(http.Body, JsonOpts)
            ?? throw new InvalidOperationException("Deluge returned malformed body");
        return parsed;
    }

    public static string? ExtractSessionCookie(string setCookie)
    {
        foreach (var part in setCookie.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("_session_id=", StringComparison.OrdinalIgnoreCase))
            {
                return part["_session_id=".Length..];
            }
        }
        return null;
    }

    public sealed class DelugeTorrent
    {
        public string? Hash { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("total_size")] public long? TotalSize { get; set; }
        public string? State { get; set; }
        public double? Progress { get; set; }
        public long? Eta { get; set; }
        [JsonPropertyName("save_path")] public string? SavePath { get; set; }
        [JsonPropertyName("error_code")] public string? ErrorCode { get; set; }
    }

    private sealed class DelugeRpcResponse
    {
        public JsonElement? Result { get; set; }
        public DelugeRpcError? Error { get; set; }
    }

    private sealed class DelugeRpcError
    {
        public int Code { get; set; }
        public string? Message { get; set; }
    }
}
