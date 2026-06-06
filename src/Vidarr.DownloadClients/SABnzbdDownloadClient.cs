using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.DownloadClients;

public sealed record SABnzbdSettings(
    Uri BaseUrl,
    string ApiKey,
    string? Category = null,
    int? Priority = null,
    TimeSpan? Timeout = null);

/// <summary>
/// SABnzbd's classic /api endpoint takes a <c>mode=</c> query string for every action.
/// The active queue (mode=queue) and completed history (mode=history) are two separate
/// collections — we surface both as DownloadClientItems with queue items reporting
/// Downloading and Completed history items reporting CompletedReadyToImport.
/// </summary>
public sealed class SABnzbdDownloadClient : IDownloadClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClient _http;
    private readonly SABnzbdSettings _settings;

    public SABnzbdDownloadClient(int id, string name, SABnzbdSettings settings, IHttpClient http)
    {
        Id = id;
        Name = name;
        _settings = settings;
        _http = http;
    }

    public int Id { get; }
    public string Name { get; }
    public DownloadProtocol Protocol => DownloadProtocol.Usenet;

    public async Task<DownloadClientItemId> DownloadAsync(RemoteRelease release, CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["mode"] = "addurl";
        qs["name"] = release.Info.SourceUrl.AbsoluteUri;
        if (!string.IsNullOrEmpty(_settings.Category)) qs["cat"] = _settings.Category;
        if (_settings.Priority is { } pri) qs["priority"] = pri.ToString(CultureInfo.InvariantCulture);
        qs["nzbname"] = release.Info.Title;

        var resp = await GetAsync(qs.ToString() ?? string.Empty, ct);
        if (resp.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"SABnzbd add failed: HTTP {resp.StatusCode}");
        }

        var doc = JsonSerializer.Deserialize<SabAddResponse>(resp.Body, JsonOpts)
            ?? throw new InvalidOperationException("SABnzbd returned malformed body");
        if (doc.Status != true)
        {
            throw new InvalidOperationException($"SABnzbd add rejected: {doc.Error ?? "unknown"}");
        }
        // SAB returns nzo_ids[] for the added item.
        var nzoId = doc.NzoIds?.FirstOrDefault()
            ?? throw new InvalidOperationException("SABnzbd add returned no nzo_id");
        return new DownloadClientItemId(nzoId);
    }

    public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct)
    {
        var queue = await FetchQueueAsync(ct);
        var history = await FetchHistoryAsync(ct);
        return [.. queue, .. history];
    }

    public async Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["mode"] = "queue";
        qs["name"] = "delete";
        qs["value"] = id.Value;
        qs["del_files"] = deleteData ? "1" : "0";
        var resp = await GetAsync(qs.ToString() ?? string.Empty, ct);

        if (resp.StatusCode is >= 200 and < 300)
        {
            return;
        }
        // If not in queue, try history delete.
        var hqs = HttpUtility.ParseQueryString(string.Empty);
        hqs["mode"] = "history";
        hqs["name"] = "delete";
        hqs["value"] = id.Value;
        hqs["del_files"] = deleteData ? "1" : "0";
        await GetAsync(hqs.ToString() ?? string.Empty, ct);
    }

    public async Task<DownloadClientTestResult> TestAsync(CancellationToken ct)
    {
        try
        {
            var qs = HttpUtility.ParseQueryString(string.Empty);
            qs["mode"] = "version";
            var resp = await GetAsync(qs.ToString() ?? string.Empty, ct);
            if (resp.StatusCode is < 200 or >= 300)
            {
                return new DownloadClientTestResult(false, $"HTTP {resp.StatusCode}");
            }
            var doc = JsonSerializer.Deserialize<SabVersionResponse>(resp.Body, JsonOpts);
            return string.IsNullOrEmpty(doc?.Version)
                ? new DownloadClientTestResult(false, "no version returned")
                : new DownloadClientTestResult(true, $"SABnzbd {doc.Version}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DownloadClientTestResult(false, ex.Message);
        }
    }

    private async Task<List<DownloadClientItem>> FetchQueueAsync(CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["mode"] = "queue";
        if (!string.IsNullOrEmpty(_settings.Category))
        {
            qs["search"] = _settings.Category;
        }
        var resp = await GetAsync(qs.ToString() ?? string.Empty, ct);
        if (resp.StatusCode != 200 || string.IsNullOrEmpty(resp.Body))
        {
            return [];
        }
        var doc = JsonSerializer.Deserialize<SabQueueResponse>(resp.Body, JsonOpts);
        if (doc?.Queue?.Slots is null)
        {
            return [];
        }
        return [.. doc.Queue.Slots.Select(MapQueueItem)];
    }

    private async Task<List<DownloadClientItem>> FetchHistoryAsync(CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["mode"] = "history";
        if (!string.IsNullOrEmpty(_settings.Category)) qs["category"] = _settings.Category;
        var resp = await GetAsync(qs.ToString() ?? string.Empty, ct);
        if (resp.StatusCode != 200 || string.IsNullOrEmpty(resp.Body))
        {
            return [];
        }
        var doc = JsonSerializer.Deserialize<SabHistoryResponse>(resp.Body, JsonOpts);
        if (doc?.History?.Slots is null)
        {
            return [];
        }
        return [.. doc.History.Slots.Select(MapHistoryItem)];
    }

    public static DownloadClientItem MapQueueItem(SabQueueSlot s)
    {
        var status = MapQueueStatus(s.Status);
        long? total = ParseMb(s.Mb);
        long? left = ParseMb(s.MbLeft);
        return new DownloadClientItem(
            Id: new DownloadClientItemId(s.NzoId ?? string.Empty),
            Title: s.Filename ?? string.Empty,
            TotalBytes: total,
            RemainingBytes: left,
            Status: status,
            OutputPath: null,
            Eta: ParseTime(s.TimeLeft),
            Message: null);
    }

    public static DownloadClientItem MapHistoryItem(SabHistorySlot s)
    {
        var status = MapHistoryStatus(s.Status);
        return new DownloadClientItem(
            Id: new DownloadClientItemId(s.NzoId ?? string.Empty),
            Title: s.Name ?? string.Empty,
            TotalBytes: s.Bytes,
            RemainingBytes: 0,
            Status: status,
            OutputPath: status is DownloadItemStatus.CompletedReadyToImport ? s.Storage : null,
            Eta: null,
            Message: string.IsNullOrEmpty(s.FailMessage) ? null : s.FailMessage);
    }

    public static DownloadItemStatus MapQueueStatus(string? state) => state switch
    {
        null or "" => DownloadItemStatus.Queued,
        "Queued" => DownloadItemStatus.Queued,
        "Paused" => DownloadItemStatus.Queued,
        "Downloading" or "Fetching" or "QuickCheck"
            or "Verifying" or "Repairing" or "Extracting" or "Moving" => DownloadItemStatus.Downloading,
        "Completed" => DownloadItemStatus.CompletedReadyToImport,
        "Failed" => DownloadItemStatus.Failed,
        _ => DownloadItemStatus.Queued,
    };

    public static DownloadItemStatus MapHistoryStatus(string? state) => state switch
    {
        "Completed" => DownloadItemStatus.CompletedReadyToImport,
        "Failed" => DownloadItemStatus.Failed,
        _ => DownloadItemStatus.Downloading,
    };

    private static long? ParseMb(string? raw) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var mb)
            ? (long)(mb * 1024 * 1024)
            : null;

    private static TimeSpan? ParseTime(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw == "0:00:00") return null;
        var parts = raw.Split(':');
        try
        {
            return parts.Length switch
            {
                3 => new TimeSpan(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture)),
                2 => new TimeSpan(0, int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture)),
                _ => null,
            };
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private Task<HttpClientResponse> GetAsync(string queryString, CancellationToken ct)
    {
        var withKey = $"{queryString}&apikey={Uri.EscapeDataString(_settings.ApiKey)}&output=json";
        var uri = new Uri(_settings.BaseUrl, $"/api?{withKey}");
        return _http.SendAsync(new HttpClientRequest(
            HttpMethod.Get,
            uri,
            new Dictionary<string, string> { ["User-Agent"] = "Vidarr/1.0" },
            Timeout: _settings.Timeout), ct);
    }

    public sealed class SabQueueSlot
    {
        [JsonPropertyName("nzo_id")] public string? NzoId { get; set; }
        public string? Filename { get; set; }
        public string? Status { get; set; }
        public string? Mb { get; set; }
        [JsonPropertyName("mbleft")] public string? MbLeft { get; set; }
        public string? TimeLeft { get; set; }
    }

    public sealed class SabHistorySlot
    {
        [JsonPropertyName("nzo_id")] public string? NzoId { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public long? Bytes { get; set; }
        public string? Storage { get; set; }
        [JsonPropertyName("fail_message")] public string? FailMessage { get; set; }
    }

    private sealed record SabAddResponse(bool? Status, [property: JsonPropertyName("nzo_ids")] List<string>? NzoIds, string? Error);
    private sealed record SabVersionResponse(string? Version);
    private sealed record SabQueueResponse(SabQueueRoot? Queue);
    private sealed record SabQueueRoot(List<SabQueueSlot>? Slots);
    private sealed record SabHistoryResponse(SabHistoryRoot? History);
    private sealed record SabHistoryRoot(List<SabHistorySlot>? Slots);
}
