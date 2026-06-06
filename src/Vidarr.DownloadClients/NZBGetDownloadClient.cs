using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.DownloadClients;

public sealed record NZBGetSettings(
    Uri BaseUrl,
    string Username,
    string Password,
    string? Category = null,
    int? Priority = null,
    TimeSpan? Timeout = null);

/// <summary>
/// NZBGet exposes a JSON-RPC endpoint at <c>/jsonrpc</c>. Auth is HTTP Basic. We use
/// <c>append</c> to add NZBs (by URL), <c>listgroups</c> for the active queue,
/// <c>history</c> for completed/failed items, and <c>editqueue</c> for deletes.
/// </summary>
public sealed class NZBGetDownloadClient : IDownloadClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClient _http;
    private readonly NZBGetSettings _settings;
    private int _rpcId;

    public NZBGetDownloadClient(int id, string name, NZBGetSettings settings, IHttpClient http)
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
        // append(NZBFilename, NZBContent, Category, Priority, AddToTop, AddPaused, DupeKey, DupeScore, DupeMode, PPParameters)
        var args = new object[]
        {
            release.Info.Title,
            release.Info.SourceUrl.AbsoluteUri,
            _settings.Category ?? string.Empty,
            _settings.Priority ?? 0,
            false,
            false,
            string.Empty,
            0,
            "SCORE",
            Array.Empty<string>(),
        };
        var response = await CallAsync("append", args, ct);
        if (response.Error is not null)
        {
            throw new InvalidOperationException($"NZBGet append failed: {response.Error.Message}");
        }
        if (response.Result is null || response.Result.Value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException("NZBGet append did not return an NZB id");
        }
        var nzbId = response.Result.Value.GetInt32();
        if (nzbId <= 0)
        {
            throw new InvalidOperationException("NZBGet append returned 0 (already exists or rejected)");
        }
        return new DownloadClientItemId(nzbId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct)
    {
        var queue = await ListGroupsAsync(ct);
        var history = await HistoryAsync(ct);
        return [.. queue, .. history];
    }

    public async Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct)
    {
        if (!int.TryParse(id.Value, out var nzbId))
        {
            return;
        }
        // editqueue(Command, Offset, EditText, IDs[])
        var command = deleteData ? "GroupFinalDelete" : "GroupDelete";
        await CallAsync("editqueue", [command, 0, string.Empty, new[] { nzbId }], ct);
    }

    public async Task<DownloadClientTestResult> TestAsync(CancellationToken ct)
    {
        try
        {
            var resp = await CallAsync("version", [], ct);
            if (resp.Error is not null)
            {
                return new DownloadClientTestResult(false, resp.Error.Message);
            }
            return resp.Result?.ValueKind == JsonValueKind.String
                ? new DownloadClientTestResult(true, $"NZBGet {resp.Result.Value.GetString()}")
                : new DownloadClientTestResult(false, "no version returned");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DownloadClientTestResult(false, ex.Message);
        }
    }

    private async Task<List<DownloadClientItem>> ListGroupsAsync(CancellationToken ct)
    {
        var resp = await CallAsync("listgroups", [0], ct);
        if (resp.Error is not null || resp.Result is null || resp.Result.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var groups = JsonSerializer.Deserialize<List<NzbGetGroup>>(resp.Result.Value.GetRawText(), JsonOpts) ?? [];
        return [.. groups
            .Where(g => string.IsNullOrEmpty(_settings.Category) || string.Equals(g.Category, _settings.Category, StringComparison.OrdinalIgnoreCase))
            .Select(MapGroup)];
    }

    private async Task<List<DownloadClientItem>> HistoryAsync(CancellationToken ct)
    {
        var resp = await CallAsync("history", [false], ct);
        if (resp.Error is not null || resp.Result is null || resp.Result.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var entries = JsonSerializer.Deserialize<List<NzbGetHistoryEntry>>(resp.Result.Value.GetRawText(), JsonOpts) ?? [];
        return [.. entries
            .Where(e => string.IsNullOrEmpty(_settings.Category) || string.Equals(e.Category, _settings.Category, StringComparison.OrdinalIgnoreCase))
            .Select(MapHistory)];
    }

    public static DownloadClientItem MapGroup(NzbGetGroup g)
    {
        var status = MapGroupStatus(g.Status);
        var total = g.FileSizeMB * 1024L * 1024L;
        var left = g.RemainingSizeMB * 1024L * 1024L;
        return new DownloadClientItem(
            Id: new DownloadClientItemId(g.NzbId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Title: g.NzbName ?? string.Empty,
            TotalBytes: total,
            RemainingBytes: left,
            Status: status,
            OutputPath: null,
            Eta: null,
            Message: null);
    }

    public static DownloadClientItem MapHistory(NzbGetHistoryEntry e)
    {
        var status = MapHistoryStatus(e.Status);
        var total = e.FileSizeMB * 1024L * 1024L;
        return new DownloadClientItem(
            Id: new DownloadClientItemId(e.NzbId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Title: e.Name ?? string.Empty,
            TotalBytes: total,
            RemainingBytes: 0,
            Status: status,
            OutputPath: status is DownloadItemStatus.CompletedReadyToImport ? e.DestDir : null,
            Eta: null,
            Message: null);
    }

    public static DownloadItemStatus MapGroupStatus(string? status) => status switch
    {
        null or "" or "QUEUED" or "PAUSED" => DownloadItemStatus.Queued,
        "DOWNLOADING" or "FETCHING" or "PP_QUEUED" or "LOADING_PARS" or "VERIFYING_SOURCES"
            or "REPAIRING" or "VERIFYING_REPAIRED" or "UNPACKING" or "MOVING" or "EXECUTING_SCRIPT" => DownloadItemStatus.Downloading,
        _ => DownloadItemStatus.Queued,
    };

    public static DownloadItemStatus MapHistoryStatus(string? status) => status switch
    {
        var s when !string.IsNullOrEmpty(s) && s.StartsWith("SUCCESS", StringComparison.Ordinal) => DownloadItemStatus.CompletedReadyToImport,
        var s when !string.IsNullOrEmpty(s) && (s.StartsWith("FAILURE", StringComparison.Ordinal) || s.StartsWith("WARNING", StringComparison.Ordinal)) => DownloadItemStatus.Failed,
        var s when !string.IsNullOrEmpty(s) && s.StartsWith("DELETED", StringComparison.Ordinal) => DownloadItemStatus.Removed,
        _ => DownloadItemStatus.Downloading,
    };

    private async Task<NzbGetRpcResponse> CallAsync(string method, object[] args, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            method,
            @params = args,
            id = System.Threading.Interlocked.Increment(ref _rpcId),
        });
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}"));
        var headers = new Dictionary<string, string>
        {
            ["User-Agent"] = "Vidarr/1.0",
            ["Authorization"] = $"Basic {creds}",
        };
        var resp = await _http.SendAsync(new HttpClientRequest(
            HttpMethod.Post,
            new Uri(_settings.BaseUrl, "/jsonrpc"),
            headers,
            new HttpClientContent.Json(payload),
            _settings.Timeout), ct);

        if (resp.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"NZBGet RPC HTTP {resp.StatusCode}");
        }
        return JsonSerializer.Deserialize<NzbGetRpcResponse>(resp.Body, JsonOpts)
            ?? throw new InvalidOperationException("NZBGet returned malformed body");
    }

    public sealed class NzbGetGroup
    {
        public int NzbId { get; set; }
        public string? NzbName { get; set; }
        public long FileSizeMB { get; set; }
        public long RemainingSizeMB { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? DestDir { get; set; }
    }

    public sealed class NzbGetHistoryEntry
    {
        public int NzbId { get; set; }
        public string? Name { get; set; }
        public long FileSizeMB { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public string? DestDir { get; set; }
    }

    private sealed class NzbGetRpcResponse
    {
        public JsonElement? Result { get; set; }
        public NzbGetRpcError? Error { get; set; }
    }

    private sealed class NzbGetRpcError
    {
        public int Code { get; set; }
        public string? Message { get; set; }
    }
}
