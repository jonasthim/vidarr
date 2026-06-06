using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;

namespace Vidarr.Notifications;

public sealed record JellyfinSettings(Uri BaseUrl, string ApiKey);

/// <summary>
/// Jellyfin and its Emby fork share the same /Library/Refresh API. We trigger a full
/// library scan on import/upgrade/delete; Jellyfin's task system queues this with a
/// debounce so frequent imports do not hammer the server. Auth via X-Emby-Token
/// (works for both Jellyfin and Emby).
/// </summary>
public sealed class JellyfinNotifier : INotification
{
    private readonly IHttpClient _http;
    private readonly JellyfinSettings _settings;

    public JellyfinNotifier(int id, string name, JellyfinSettings settings, IReadOnlySet<NotificationEventType> supportedEvents, IHttpClient http)
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

    public Task OnGrabAsync(GrabEvent evt, CancellationToken ct) => Task.CompletedTask;
    public Task OnImportAsync(ImportEvent evt, CancellationToken ct) => RefreshAsync(ct);
    public Task OnUpgradeAsync(UpgradeEvent evt, CancellationToken ct) => RefreshAsync(ct);
    public Task OnDeleteAsync(DeleteEvent evt, CancellationToken ct) => RefreshAsync(ct);
    public Task OnHealthIssueAsync(HealthIssueEvent evt, CancellationToken ct) => Task.CompletedTask;

    public async Task<NotificationTestResult> OnTestAsync(CancellationToken ct)
    {
        try
        {
            // System/Info/Public requires no auth — confirms the base URL reaches a Jellyfin/Emby instance.
            var resp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Get,
                new Uri(_settings.BaseUrl, "/System/Info/Public"), BuildHeaders()), ct);
            return resp.StatusCode is >= 200 and < 300
                ? new NotificationTestResult(true, "Jellyfin/Emby reachable")
                : new NotificationTestResult(false, $"HTTP {resp.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new NotificationTestResult(false, ex.Message);
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var resp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Post,
            new Uri(_settings.BaseUrl, "/Library/Refresh"), BuildHeaders(),
            new HttpClientContent.Json("{}")), ct);
        if (resp.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"Jellyfin refresh failed: HTTP {resp.StatusCode}");
        }
    }

    private Dictionary<string, string> BuildHeaders() => new()
    {
        ["User-Agent"] = "Vidarr/1.0",
        ["Accept"] = "application/json",
        ["X-Emby-Token"] = _settings.ApiKey,
    };
}
