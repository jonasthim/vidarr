using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.Notifications;

public interface INotificationFactory
{
    string Implementation { get; }
    string DisplayName { get; }
    IReadOnlyList<NotificationFieldSchema> SettingsSchema { get; }
    IReadOnlyList<NotificationEventType> SupportedEvents { get; }
    INotification Create(int id, string name, string settingsJson, IReadOnlySet<NotificationEventType> subscribedEvents);
}

public sealed record NotificationFieldSchema(string Name, string Label, string Type, bool Required, string? HelpText = null);

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Trivial cached JsonSerializerOptions singleton; exercised transitively by every factory test.")]
internal static class NotificationJsonOptions
{
    public static readonly JsonSerializerOptions Default = new() { PropertyNameCaseInsensitive = true };
}

public sealed class WebhookFactory : INotificationFactory
{
    private readonly IHttpClient _http;
    public WebhookFactory(IHttpClient http) { _http = http; }

    public string Implementation => "Webhook";
    public string DisplayName => "Generic webhook";
    public IReadOnlyList<NotificationFieldSchema> SettingsSchema { get; } =
    [
        new("url", "Webhook URL", "url", true),
    ];
    public IReadOnlyList<NotificationEventType> SupportedEvents { get; } =
    [
        NotificationEventType.OnGrab, NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
        NotificationEventType.OnDelete, NotificationEventType.OnHealthIssue, NotificationEventType.OnTest,
    ];

    public INotification Create(int id, string name, string settingsJson, IReadOnlySet<NotificationEventType> subscribedEvents)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, NotificationJsonOptions.Default) ?? new RawSettings();
        return new WebhookNotifier(id, name,
            new WebhookSettings(new Uri(raw.Url ?? "https://example.invalid"), new Dictionary<string, string>()),
            subscribedEvents, _http);
    }

    private sealed class RawSettings { public string? Url { get; set; } }
}

public sealed class PlexFactory : INotificationFactory
{
    private readonly IHttpClient _http;
    public PlexFactory(IHttpClient http) { _http = http; }

    public string Implementation => "Plex";
    public string DisplayName => "Plex Media Server";
    public IReadOnlyList<NotificationFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "Server URL", "url", true, "e.g. http://localhost:32400"),
        new("token", "X-Plex-Token", "password", true),
        new("librarySectionId", "Library section id", "number", false, "Section to refresh on import"),
    ];
    public IReadOnlyList<NotificationEventType> SupportedEvents { get; } =
    [
        NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
        NotificationEventType.OnDelete, NotificationEventType.OnTest,
    ];

    public INotification Create(int id, string name, string settingsJson, IReadOnlySet<NotificationEventType> subscribedEvents)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, NotificationJsonOptions.Default) ?? new RawSettings();
        return new PlexNotifier(id, name,
            new PlexSettings(new Uri(raw.BaseUrl ?? "http://localhost:32400"), raw.Token ?? string.Empty, raw.LibrarySectionId),
            subscribedEvents, _http);
    }

    private sealed class RawSettings
    {
        public string? BaseUrl { get; set; }
        public string? Token { get; set; }
        public int? LibrarySectionId { get; set; }
    }
}

public sealed class JellyfinFactory : INotificationFactory
{
    private readonly IHttpClient _http;
    public JellyfinFactory(IHttpClient http) { _http = http; }

    public string Implementation => "Jellyfin";
    public string DisplayName => "Jellyfin / Emby";
    public IReadOnlyList<NotificationFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "Server URL", "url", true, "e.g. http://localhost:8096"),
        new("apiKey", "API key", "password", true),
    ];
    public IReadOnlyList<NotificationEventType> SupportedEvents { get; } =
    [
        NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
        NotificationEventType.OnDelete, NotificationEventType.OnTest,
    ];

    public INotification Create(int id, string name, string settingsJson, IReadOnlySet<NotificationEventType> subscribedEvents)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, NotificationJsonOptions.Default) ?? new RawSettings();
        return new JellyfinNotifier(id, name,
            new JellyfinSettings(new Uri(raw.BaseUrl ?? "http://localhost:8096"), raw.ApiKey ?? string.Empty),
            subscribedEvents, _http);
    }

    private sealed class RawSettings
    {
        public string? BaseUrl { get; set; }
        public string? ApiKey { get; set; }
    }
}

public sealed class DiscordFactory : INotificationFactory
{
    private readonly IHttpClient _http;
    public DiscordFactory(IHttpClient http) { _http = http; }

    public string Implementation => "Discord";
    public string DisplayName => "Discord webhook";
    public IReadOnlyList<NotificationFieldSchema> SettingsSchema { get; } =
    [
        new("webhookUrl", "Webhook URL", "url", true),
        new("username", "Username override", "text", false),
        new("avatarUrl", "Avatar URL override", "url", false),
    ];
    public IReadOnlyList<NotificationEventType> SupportedEvents { get; } =
    [
        NotificationEventType.OnGrab, NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
        NotificationEventType.OnDelete, NotificationEventType.OnHealthIssue, NotificationEventType.OnTest,
    ];

    public INotification Create(int id, string name, string settingsJson, IReadOnlySet<NotificationEventType> subscribedEvents)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, NotificationJsonOptions.Default) ?? new RawSettings();
        return new DiscordNotifier(id, name,
            new DiscordSettings(new Uri(raw.WebhookUrl ?? "https://discord.com/api/webhooks/invalid"),
                raw.Username, raw.AvatarUrl),
            subscribedEvents, _http);
    }

    private sealed class RawSettings
    {
        public string? WebhookUrl { get; set; }
        public string? Username { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
