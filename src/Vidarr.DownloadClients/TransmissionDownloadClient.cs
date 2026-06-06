using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.DownloadClients;

public sealed record TransmissionSettings(
    Uri BaseUrl,
    string? Username = null,
    string? Password = null,
    string? Category = null,
    string? DownloadDir = null,
    TimeSpan? Timeout = null);

/// <summary>
/// Transmission RPC. Authenticates via Basic + a per-host session id returned through
/// the <c>X-Transmission-Session-Id</c> response header on a 409. We perform the standard
/// "409 dance": send a request, learn the session id from the response, retry once with
/// the header set.
/// </summary>
public sealed class TransmissionDownloadClient : IDownloadClient
{
    private const string SessionHeader = "X-Transmission-Session-Id";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClient _http;
    private readonly TransmissionSettings _settings;
    private string? _sessionId;

    public TransmissionDownloadClient(int id, string name, TransmissionSettings settings, IHttpClient http)
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
        var args = new Dictionary<string, object?>
        {
            ["filename"] = release.Info.Magnet ?? release.Info.SourceUrl.AbsoluteUri,
            ["paused"] = false,
        };
        if (!string.IsNullOrEmpty(_settings.DownloadDir))
        {
            args["download-dir"] = _settings.DownloadDir;
        }

        var response = await CallAsync("torrent-add", args, ct);
        // Transmission returns { result: "success", arguments: { torrent-added: { hashString, id, name } } }
        // or { result: "success", arguments: { torrent-duplicate: {...} } } when the torrent already exists.
        var doc = JsonSerializer.Deserialize<TransmissionAddResponse>(response, JsonOpts)
            ?? throw new InvalidOperationException("Transmission returned malformed body");
        if (!string.Equals(doc.Result, "success", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Transmission add failed: {doc.Result}");
        }
        var added = doc.Arguments?.TorrentAdded ?? doc.Arguments?.TorrentDuplicate
            ?? throw new InvalidOperationException("Transmission add succeeded but returned no torrent");
        return new DownloadClientItemId(added.HashString ?? added.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct)
    {
        var args = new Dictionary<string, object?>
        {
            ["fields"] = new[]
            {
                "id", "hashString", "name", "totalSize", "leftUntilDone", "status",
                "downloadDir", "eta", "errorString",
            },
        };
        var response = await CallAsync("torrent-get", args, ct);
        var doc = JsonSerializer.Deserialize<TransmissionGetResponse>(response, JsonOpts)
            ?? new TransmissionGetResponse(null, null);
        if (doc.Arguments?.Torrents is null)
        {
            return [];
        }
        return [.. doc.Arguments.Torrents.Select(MapItem)];
    }

    public async Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct)
    {
        var args = new Dictionary<string, object?>
        {
            ["ids"] = new[] { id.Value },
            ["delete-local-data"] = deleteData,
        };
        await CallAsync("torrent-remove", args, ct);
    }

    public async Task<DownloadClientTestResult> TestAsync(CancellationToken ct)
    {
        try
        {
            var response = await CallAsync("session-get", null, ct);
            var doc = JsonSerializer.Deserialize<TransmissionGenericResponse>(response, JsonOpts);
            return string.Equals(doc?.Result, "success", StringComparison.Ordinal)
                ? new DownloadClientTestResult(true, "OK")
                : new DownloadClientTestResult(false, doc?.Result ?? "unknown");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DownloadClientTestResult(false, ex.Message);
        }
    }

    public static DownloadClientItem MapItem(TransmissionTorrent t)
    {
        var status = MapStatus(t.Status, t.LeftUntilDone);
        return new DownloadClientItem(
            Id: new DownloadClientItemId(t.HashString ?? t.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Title: t.Name ?? string.Empty,
            TotalBytes: t.TotalSize,
            RemainingBytes: t.LeftUntilDone,
            Status: status,
            OutputPath: status is DownloadItemStatus.CompletedReadyToImport or DownloadItemStatus.Imported
                ? t.DownloadDir
                : null,
            Eta: t.Eta is > 0 ? TimeSpan.FromSeconds(t.Eta.Value) : null,
            Message: string.IsNullOrEmpty(t.ErrorString) ? null : t.ErrorString);
    }

    /// <summary>
    /// Transmission status codes (per the RPC spec):
    ///   0 stopped, 1 check-pending, 2 checking, 3 download-pending,
    ///   4 downloading, 5 seed-pending, 6 seeding
    /// </summary>
    public static DownloadItemStatus MapStatus(int status, long? leftUntilDone) => status switch
    {
        0 => DownloadItemStatus.Queued,
        1 or 2 or 3 => DownloadItemStatus.Downloading,
        4 => DownloadItemStatus.Downloading,
        5 or 6 => leftUntilDone is null or 0 ? DownloadItemStatus.CompletedReadyToImport : DownloadItemStatus.Downloading,
        _ => DownloadItemStatus.Queued,
    };

    private async Task<string> CallAsync(string method, object? arguments, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { method, arguments });
        var first = await SendRpcAsync(payload, ct);

        if (first.StatusCode == 409)
        {
            _sessionId = first.Headers.TryGetValue(SessionHeader, out var sid) ? sid : null;
            var second = await SendRpcAsync(payload, ct);
            if (second.StatusCode is < 200 or >= 300)
            {
                throw new InvalidOperationException($"Transmission RPC failed: HTTP {second.StatusCode}");
            }
            return second.Body;
        }

        if (first.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"Transmission RPC failed: HTTP {first.StatusCode}");
        }
        return first.Body;
    }

    private Task<HttpClientResponse> SendRpcAsync(string payload, CancellationToken ct)
    {
        var headers = new Dictionary<string, string>
        {
            ["User-Agent"] = "Vidarr/1.0",
            ["Content-Type"] = "application/json",
        };
        if (!string.IsNullOrEmpty(_sessionId))
        {
            headers[SessionHeader] = _sessionId;
        }
        if (!string.IsNullOrEmpty(_settings.Username))
        {
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}"));
            headers["Authorization"] = $"Basic {creds}";
        }

        return _http.SendAsync(new HttpClientRequest(
            HttpMethod.Post,
            new Uri(_settings.BaseUrl, "/transmission/rpc"),
            headers,
            new HttpClientContent.Json(payload),
            _settings.Timeout), ct);
    }

    public sealed class TransmissionTorrent
    {
        public int Id { get; set; }
        [JsonPropertyName("hashString")] public string? HashString { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("totalSize")] public long TotalSize { get; set; }
        [JsonPropertyName("leftUntilDone")] public long? LeftUntilDone { get; set; }
        public int Status { get; set; }
        [JsonPropertyName("downloadDir")] public string? DownloadDir { get; set; }
        public long? Eta { get; set; }
        [JsonPropertyName("errorString")] public string? ErrorString { get; set; }
    }

    private sealed record TransmissionAddResponse(string? Result, TransmissionAddArguments? Arguments);
    private sealed record TransmissionAddArguments(
        [property: JsonPropertyName("torrent-added")] TransmissionAddTorrent? TorrentAdded,
        [property: JsonPropertyName("torrent-duplicate")] TransmissionAddTorrent? TorrentDuplicate);
    private sealed record TransmissionAddTorrent(int Id, string? HashString, string? Name);
    private sealed record TransmissionGetResponse(string? Result, TransmissionGetArguments? Arguments);
    private sealed record TransmissionGetArguments(List<TransmissionTorrent>? Torrents);
    private sealed record TransmissionGenericResponse(string? Result);
}
