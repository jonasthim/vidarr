using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;

namespace Vidarr.Notifications;

public sealed class WebhookNotifier : INotification
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly IHttpClient _http;
    private readonly WebhookSettings _settings;

    public WebhookNotifier(int id, string name, WebhookSettings settings, IReadOnlySet<NotificationEventType> supportedEvents, IHttpClient http)
    {
        Id = id;
        Name = name;
        _settings = settings;
        SupportedEvents = supportedEvents;
        _http = http;
    }

    public int Id { get; }
    public string Name { get; }
    public IReadOnlySet<NotificationEventType> SupportedEvents { get; }

    public Task OnGrabAsync(GrabEvent evt, CancellationToken ct) => PostAsync("grab", evt, ct);
    public Task OnImportAsync(ImportEvent evt, CancellationToken ct) => PostAsync("import", evt, ct);
    public Task OnUpgradeAsync(UpgradeEvent evt, CancellationToken ct) => PostAsync("upgrade", evt, ct);
    public Task OnDeleteAsync(DeleteEvent evt, CancellationToken ct) => PostAsync("delete", evt, ct);
    public Task OnHealthIssueAsync(HealthIssueEvent evt, CancellationToken ct) => PostAsync("healthIssue", evt, ct);

    public async Task<NotificationTestResult> OnTestAsync(CancellationToken ct)
    {
        var resp = await PostRawAsync(new { eventType = "test", source = "vidarr" }, ct);
        return resp.StatusCode is >= 200 and < 300
            ? new NotificationTestResult(true, $"HTTP {resp.StatusCode}")
            : new NotificationTestResult(false, $"HTTP {resp.StatusCode}: {Truncate(resp.Body, 200)}");
    }

    private Task<HttpClientResponse> PostAsync<T>(string eventType, T payload, CancellationToken ct) =>
        PostRawAsync(new { eventType, data = payload }, ct);

    private async Task<HttpClientResponse> PostRawAsync(object payload, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(payload, JsonOpts);
        var headers = new Dictionary<string, string> { ["User-Agent"] = "Vidarr/1.0" };
        foreach (var (k, v) in _settings.Headers)
        {
            headers[k] = v;
        }

        var request = new HttpClientRequest(
            HttpMethod.Post,
            _settings.Url,
            headers,
            new HttpClientContent.Json(body));
        return await _http.SendAsync(request, ct);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : string.Concat(s.AsSpan(0, max), "...");
}

public sealed record WebhookSettings(Uri Url, IReadOnlyDictionary<string, string> Headers);
