using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;

namespace Vidarr.Notifications;

public sealed record DiscordSettings(Uri WebhookUrl, string? Username = null, string? AvatarUrl = null);

public sealed class DiscordNotifier : INotification
{
    private const int ColorGreen = 0x2ECC71;
    private const int ColorBlue = 0x3498DB;
    private const int ColorYellow = 0xF1C40F;
    private const int ColorRed = 0xE74C3C;
    private const int ColorGrey = 0x95A5A6;

    private readonly IHttpClient _http;
    private readonly DiscordSettings _settings;

    public DiscordNotifier(int id, string name, DiscordSettings settings, IReadOnlySet<NotificationEventType> supportedEvents, IHttpClient http)
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

    public Task OnGrabAsync(GrabEvent evt, CancellationToken ct) => PostEmbedAsync(BuildEmbed(
        title: $"Grabbed: {evt.ReleaseTitle}",
        color: ColorBlue,
        fields:
        [
            ("Indexer", evt.IndexerName),
            ("Download client", evt.DownloadClientName),
            ("Quality", evt.Quality.Name),
        ]), ct);

    public Task OnImportAsync(ImportEvent evt, CancellationToken ct) => PostEmbedAsync(BuildEmbed(
        title: $"Imported (artist #{evt.ArtistId}, video #{evt.MusicVideoId})",
        color: ColorGreen,
        fields:
        [
            ("File", evt.FilePath),
            ("Quality", evt.Quality.Name),
            ("Size", FormatBytes(evt.SizeBytes)),
            ("Source", evt.SourceLabel ?? "—"),
        ]), ct);

    public Task OnUpgradeAsync(UpgradeEvent evt, CancellationToken ct) => PostEmbedAsync(BuildEmbed(
        title: $"Upgraded (video #{evt.MusicVideoId})",
        color: ColorYellow,
        fields:
        [
            ("From", evt.OldQuality.Name),
            ("To", evt.NewQuality.Name),
            ("File", evt.FilePath),
        ]), ct);

    public Task OnDeleteAsync(DeleteEvent evt, CancellationToken ct) => PostEmbedAsync(BuildEmbed(
        title: $"Deleted (video #{evt.MusicVideoId})",
        color: ColorGrey,
        fields: [("File", evt.FilePath)]), ct);

    public Task OnHealthIssueAsync(HealthIssueEvent evt, CancellationToken ct) => PostEmbedAsync(BuildEmbed(
        title: $"Health issue: {evt.Source}",
        color: evt.Severity == HealthSeverity.Error ? ColorRed : ColorYellow,
        fields:
        [
            ("Severity", evt.Severity.ToString()),
            ("Message", evt.Message),
            ("Resolved", evt.Resolved ? "yes" : "no"),
        ]), ct);

    public async Task<NotificationTestResult> OnTestAsync(CancellationToken ct)
    {
        try
        {
            var resp = await PostEmbedAsync(BuildEmbed(
                title: "Vidarr test notification",
                color: ColorGrey,
                fields: [("Source", "vidarr"), ("Status", "ok")]), ct);
            return resp.StatusCode is >= 200 and < 300
                ? new NotificationTestResult(true, "Discord webhook accepted")
                : new NotificationTestResult(false, $"HTTP {resp.StatusCode}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new NotificationTestResult(false, ex.Message);
        }
    }

    public static object BuildEmbed(string title, int color, IReadOnlyList<(string Name, string Value)> fields) =>
        new
        {
            title,
            color,
            fields = fields.Select(f => new { name = f.Name, value = f.Value, inline = true }).ToArray(),
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
        };

    private Task<HttpClientResponse> PostEmbedAsync(object embed, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            username = _settings.Username,
            avatar_url = _settings.AvatarUrl,
            embeds = new[] { embed },
        });
        return _http.SendAsync(new HttpClientRequest(
            HttpMethod.Post,
            _settings.WebhookUrl,
            new Dictionary<string, string> { ["User-Agent"] = "Vidarr/1.0" },
            new HttpClientContent.Json(payload)), ct);
    }

    private static string FormatBytes(long n) =>
        n >= 1_000_000_000 ? $"{n / 1_000_000_000.0:0.##} GB"
        : n >= 1_000_000 ? $"{n / 1_000_000.0:0.#} MB"
        : $"{n} B";
}
