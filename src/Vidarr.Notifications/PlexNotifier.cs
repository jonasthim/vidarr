using System.Globalization;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;

namespace Vidarr.Notifications;

public sealed record PlexSettings(Uri BaseUrl, string Token, int? LibrarySectionId = null);

/// <summary>
/// Plex Media Server triggers a section refresh by PUT-ing to /library/sections/{id}/refresh.
/// We only need to know the library section to refresh (configured by the user) — the
/// actual import payload is irrelevant: Plex re-scans the library on its own.
/// </summary>
public sealed class PlexNotifier : INotification
{
    private readonly IHttpClient _http;
    private readonly PlexSettings _settings;

    public PlexNotifier(int id, string name, PlexSettings settings, IReadOnlySet<NotificationEventType> supportedEvents, IHttpClient http)
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

    public Task OnGrabAsync(GrabEvent evt, CancellationToken ct) => Task.CompletedTask;            // Plex only cares about completed files
    public Task OnImportAsync(ImportEvent evt, CancellationToken ct) => RefreshSectionAsync(ct);
    public Task OnUpgradeAsync(UpgradeEvent evt, CancellationToken ct) => RefreshSectionAsync(ct);
    public Task OnDeleteAsync(DeleteEvent evt, CancellationToken ct) => RefreshSectionAsync(ct);
    public Task OnHealthIssueAsync(HealthIssueEvent evt, CancellationToken ct) => Task.CompletedTask;

    public async Task<NotificationTestResult> OnTestAsync(CancellationToken ct)
    {
        try
        {
            var uri = BuildSectionsUri();
            var resp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Get, uri, BuildHeaders()), ct);
            return resp.StatusCode is >= 200 and < 300
                ? new NotificationTestResult(true, "Plex sections OK")
                : new NotificationTestResult(false, $"HTTP {resp.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new NotificationTestResult(false, ex.Message);
        }
    }

    private async Task RefreshSectionAsync(CancellationToken ct)
    {
        if (_settings.LibrarySectionId is not { } sectionId)
        {
            return;
        }
        var uri = new Uri(_settings.BaseUrl,
            $"/library/sections/{sectionId.ToString(CultureInfo.InvariantCulture)}/refresh");
        var resp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Put, uri, BuildHeaders()), ct);
        if (resp.StatusCode is < 200 or >= 300)
        {
            throw new InvalidOperationException($"Plex refresh failed: HTTP {resp.StatusCode}");
        }
    }

    private Uri BuildSectionsUri() => new(_settings.BaseUrl, "/library/sections");

    private Dictionary<string, string> BuildHeaders() => new()
    {
        ["User-Agent"] = "Vidarr/1.0",
        ["Accept"] = "application/json",
        ["X-Plex-Token"] = _settings.Token,
    };
}
