using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;

namespace Vidarr.Indexers;

public interface IIndexerFactory
{
    string Implementation { get; }
    string DisplayName { get; }
    /// <summary>JSON schema describing the settings fields the UI should render.</summary>
    IReadOnlyList<IndexerFieldSchema> SettingsSchema { get; }
    IIndexer Create(int id, string name, string settingsJson);
}

public sealed record IndexerFieldSchema(string Name, string Label, string Type, bool Required, string? HelpText = null);

public sealed class NewznabIndexerFactory : IIndexerFactory
{
    private readonly IHttpClient _http;

    public NewznabIndexerFactory(IHttpClient http) { _http = http; }

    public string Implementation => "Newznab";
    public string DisplayName => "NewzNab (Usenet)";

    public IReadOnlyList<IndexerFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "URL", "url", true, "e.g. https://api.nzbgeek.info"),
        new("apiKey", "API key", "password", true, null),
        new("categories", "Categories (comma-separated newznab IDs)", "text", false, "Default: 6030 (Music Video)"),
        new("minAgeMinutes", "Minimum age (minutes)", "number", false, null),
        new("maxAgeDays", "Maximum age (days)", "number", false, null),
    ];

    public IIndexer Create(int id, string name, string settingsJson) =>
        new NewznabIndexer(id, name, ParseSettings(settingsJson), _http);

    internal static NewznabIndexerSettings ParseSettings(string json)
    {
        var raw = JsonSerializer.Deserialize<RawNewznabSettings>(json) ?? new RawNewznabSettings();
        return new NewznabIndexerSettings(
            BaseUrl: new Uri(string.IsNullOrWhiteSpace(raw.BaseUrl) ? "https://example.invalid" : raw.BaseUrl, UriKind.Absolute),
            ApiKey: raw.ApiKey,
            Categories: raw.Categories?.Count > 0 ? raw.Categories : [6030],
            MinAgeMinutes: raw.MinAgeMinutes,
            MaxAgeDays: raw.MaxAgeDays);
    }

    private sealed class RawNewznabSettings
    {
        public string? BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public List<int>? Categories { get; set; }
        public int? MinAgeMinutes { get; set; }
        public int? MaxAgeDays { get; set; }
    }
}

public sealed class TorznabIndexerFactory : IIndexerFactory
{
    private readonly IHttpClient _http;
    public TorznabIndexerFactory(IHttpClient http) { _http = http; }
    public string Implementation => "Torznab";
    public string DisplayName => "Torznab (Torrent)";

    public IReadOnlyList<IndexerFieldSchema> SettingsSchema { get; } =
    [
        new("baseUrl", "URL", "url", true, "e.g. http://localhost:9117/api/v2.0/indexers/all/results/torznab"),
        new("apiKey", "API key", "password", true, null),
        new("categories", "Categories (comma-separated torznab IDs)", "text", false, "Default: 6030 (Music Video)"),
        new("minAgeMinutes", "Minimum age (minutes)", "number", false, null),
        new("maxAgeDays", "Maximum age (days)", "number", false, null),
    ];

    public IIndexer Create(int id, string name, string settingsJson) =>
        new TorznabIndexer(id, name, NewznabIndexerFactory.ParseSettings(settingsJson), _http);
}

public sealed class YouTubeIndexerFactory : IIndexerFactory
{
    private readonly IProcessRunner _processes;
    public YouTubeIndexerFactory(IProcessRunner processes) { _processes = processes; }

    public string Implementation => "YouTube";
    public string DisplayName => "YouTube (yt-dlp)";

    public IReadOnlyList<IndexerFieldSchema> SettingsSchema { get; } =
    [
        new("channelIds", "Monitored channel IDs (comma-separated UC...)", "text", false, "Used for RSS sync"),
        new("maxResults", "Max results per search", "number", false, "Default 10"),
        new("rssBatchSize", "RSS batch size per channel", "number", false, "Default 15"),
    ];

    public IIndexer Create(int id, string name, string settingsJson)
    {
        var raw = JsonSerializer.Deserialize<RawYoutubeSettings>(settingsJson) ?? new RawYoutubeSettings();
        var settings = YouTubeIndexerSettings.Default with
        {
            ChannelIds = raw.ChannelIds ?? [],
            MaxResults = raw.MaxResults ?? 10,
            RssBatchSize = raw.RssBatchSize ?? 15,
        };
        return new YouTubeIndexer(id, name, settings, _processes);
    }

    private sealed class RawYoutubeSettings
    {
        public List<string>? ChannelIds { get; set; }
        public int? MaxResults { get; set; }
        public int? RssBatchSize { get; set; }
    }
}
