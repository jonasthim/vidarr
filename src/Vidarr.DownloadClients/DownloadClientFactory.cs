using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.DownloadClients;

internal static class FactoryJsonOptions
{
    public static readonly JsonSerializerOptions Default = new() { PropertyNameCaseInsensitive = true };
}

public interface IDownloadClientFactory
{
    string Implementation { get; }
    string DisplayName { get; }
    DownloadProtocol Protocol { get; }
    IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; }
    IDownloadClient Create(int id, string name, string settingsJson);
}

public sealed record DownloadClientFieldSchema(string Name, string Label, string Type, bool Required, string? HelpText = null);

public sealed class QBittorrentFactory : IDownloadClientFactory
{
    private readonly IHttpClient _http;
    public QBittorrentFactory(IHttpClient http) { _http = http; }

    public string Implementation => "QBittorrent";
    public string DisplayName => "qBittorrent";
    public DownloadProtocol Protocol => DownloadProtocol.Torrent;

    public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "URL", "url", true, "e.g. http://localhost:8080"),
        new("username", "Username", "text", true),
        new("password", "Password", "password", true),
        new("category", "Category", "text", false, "qBit category that owns Vidarr downloads"),
    ];

    public IDownloadClient Create(int id, string name, string settingsJson)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, FactoryJsonOptions.Default) ?? new RawSettings();
        return new QBittorrentDownloadClient(id, name,
            new QBittorrentSettings(
                BaseUrl: new Uri(string.IsNullOrWhiteSpace(raw.BaseUrl) ? "http://localhost:8080" : raw.BaseUrl, UriKind.Absolute),
                Username: raw.Username ?? string.Empty,
                Password: raw.Password ?? string.Empty,
                Category: raw.Category),
            _http);
    }

    private sealed class RawSettings
    {
        public string? BaseUrl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Category { get; set; }
    }
}

public sealed class TransmissionFactory : IDownloadClientFactory
{
    private readonly IHttpClient _http;
    public TransmissionFactory(IHttpClient http) { _http = http; }

    public string Implementation => "Transmission";
    public string DisplayName => "Transmission";
    public DownloadProtocol Protocol => DownloadProtocol.Torrent;

    public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "URL", "url", true, "e.g. http://localhost:9091"),
        new("username", "Username", "text", false),
        new("password", "Password", "password", false),
        new("downloadDir", "Download directory", "text", false),
    ];

    public IDownloadClient Create(int id, string name, string settingsJson)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, FactoryJsonOptions.Default) ?? new RawSettings();
        return new TransmissionDownloadClient(id, name,
            new TransmissionSettings(
                BaseUrl: new Uri(string.IsNullOrWhiteSpace(raw.BaseUrl) ? "http://localhost:9091" : raw.BaseUrl, UriKind.Absolute),
                Username: raw.Username,
                Password: raw.Password,
                DownloadDir: raw.DownloadDir),
            _http);
    }

    private sealed class RawSettings
    {
        public string? BaseUrl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? DownloadDir { get; set; }
    }
}

public sealed class DelugeFactory : IDownloadClientFactory
{
    private readonly IHttpClient _http;
    public DelugeFactory(IHttpClient http) { _http = http; }

    public string Implementation => "Deluge";
    public string DisplayName => "Deluge";
    public DownloadProtocol Protocol => DownloadProtocol.Torrent;

    public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "URL", "url", true, "e.g. http://localhost:8112"),
        new("password", "WebUI password", "password", true),
        new("category", "Label", "text", false),
        new("downloadLocation", "Download location", "text", false),
    ];

    public IDownloadClient Create(int id, string name, string settingsJson)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, FactoryJsonOptions.Default) ?? new RawSettings();
        return new DelugeDownloadClient(id, name,
            new DelugeSettings(
                BaseUrl: new Uri(string.IsNullOrWhiteSpace(raw.BaseUrl) ? "http://localhost:8112" : raw.BaseUrl, UriKind.Absolute),
                Password: raw.Password ?? string.Empty,
                Category: raw.Category,
                DownloadLocation: raw.DownloadLocation),
            _http);
    }

    private sealed class RawSettings
    {
        public string? BaseUrl { get; set; }
        public string? Password { get; set; }
        public string? Category { get; set; }
        public string? DownloadLocation { get; set; }
    }
}

public sealed class SABnzbdFactory : IDownloadClientFactory
{
    private readonly IHttpClient _http;
    public SABnzbdFactory(IHttpClient http) { _http = http; }

    public string Implementation => "SABnzbd";
    public string DisplayName => "SABnzbd";
    public DownloadProtocol Protocol => DownloadProtocol.Usenet;

    public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "URL", "url", true, "e.g. http://localhost:8080"),
        new("apiKey", "API key", "password", true),
        new("category", "Category", "text", false),
        new("priority", "Priority", "number", false, "-1=Low, 0=Normal, 1=High, 2=Force"),
    ];

    public IDownloadClient Create(int id, string name, string settingsJson)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, FactoryJsonOptions.Default) ?? new RawSettings();
        return new SABnzbdDownloadClient(id, name,
            new SABnzbdSettings(
                BaseUrl: new Uri(string.IsNullOrWhiteSpace(raw.BaseUrl) ? "http://localhost:8080" : raw.BaseUrl, UriKind.Absolute),
                ApiKey: raw.ApiKey ?? string.Empty,
                Category: raw.Category,
                Priority: raw.Priority),
            _http);
    }

    private sealed class RawSettings
    {
        public string? BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? Category { get; set; }
        public int? Priority { get; set; }
    }
}

public sealed class NZBGetFactory : IDownloadClientFactory
{
    private readonly IHttpClient _http;
    public NZBGetFactory(IHttpClient http) { _http = http; }

    public string Implementation => "NZBGet";
    public string DisplayName => "NZBGet";
    public DownloadProtocol Protocol => DownloadProtocol.Usenet;

    public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "URL", "url", true, "e.g. http://localhost:6789"),
        new("username", "Username", "text", true, "Default: nzbget"),
        new("password", "Password", "password", true),
        new("category", "Category", "text", false),
        new("priority", "Priority", "number", false, "-100..900"),
    ];

    public IDownloadClient Create(int id, string name, string settingsJson)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, FactoryJsonOptions.Default) ?? new RawSettings();
        return new NZBGetDownloadClient(id, name,
            new NZBGetSettings(
                BaseUrl: new Uri(string.IsNullOrWhiteSpace(raw.BaseUrl) ? "http://localhost:6789" : raw.BaseUrl, UriKind.Absolute),
                Username: raw.Username ?? "nzbget",
                Password: raw.Password ?? string.Empty,
                Category: raw.Category,
                Priority: raw.Priority),
            _http);
    }

    private sealed class RawSettings
    {
        public string? BaseUrl { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Category { get; set; }
        public int? Priority { get; set; }
    }
}

public sealed class YtDlpFactory : IDownloadClientFactory
{
    private readonly IProcessRunner _processes;
    private readonly IFileSystem _fileSystem;
    public YtDlpFactory(IProcessRunner processes, IFileSystem fileSystem)
    {
        _processes = processes;
        _fileSystem = fileSystem;
    }

    public string Implementation => "YtDlp";
    public string DisplayName => "yt-dlp";
    public DownloadProtocol Protocol => DownloadProtocol.Streaming;

    public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } =
    [
        new("incompleteFolder", "Incomplete folder", "text", true, "Working directory for active downloads"),
        new("formatSelector", "yt-dlp format selector", "text", false, "Default: bv*+ba/b"),
        new("outputContainer", "Output container", "text", false, "Default: mkv"),
    ];

    public IDownloadClient Create(int id, string name, string settingsJson)
    {
        var raw = JsonSerializer.Deserialize<RawSettings>(settingsJson, FactoryJsonOptions.Default) ?? new RawSettings();
        var settings = new YtDlpDownloadClientSettings(
            IncompleteFolder: raw.IncompleteFolder ?? "/tmp/vidarr-incomplete",
            FormatSelector: raw.FormatSelector ?? "bv*+ba/b",
            OutputContainer: raw.OutputContainer ?? "mkv");
        return new YtDlpDownloadClient(id, name, settings, _processes, _fileSystem);
    }

    private sealed class RawSettings
    {
        public string? IncompleteFolder { get; set; }
        public string? FormatSelector { get; set; }
        public string? OutputContainer { get; set; }
    }
}
