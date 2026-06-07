namespace Vidarr.Catalog.Entities;

/// <summary>
/// Persisted configuration of a runtime IIndexer instance.
/// Implementation field selects the concrete provider (Newznab/Torznab/YouTube);
/// SettingsJson is impl-specific (URL, API key, category map, channel IDs, ...).
/// </summary>
public sealed class IndexerConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Implementation { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public int Priority { get; set; } = 25;
    public bool EnableRss { get; set; } = true;
    public bool EnableAutomaticSearch { get; set; } = true;
    public bool EnableInteractiveSearch { get; set; } = true;
    public int? PreferredDownloadClientId { get; set; }
    public string TagsJson { get; set; } = "[]";
}

public sealed class DownloadClientConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Implementation { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public int Priority { get; set; } = 1;
    public bool Enable { get; set; } = true;
    public string? Category { get; set; }
    public bool RemovesCompletedDownloads { get; set; }
    public string TagsJson { get; set; } = "[]";
}

public sealed class NotificationConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Implementation { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public bool Enable { get; set; } = true;

    /// <summary>JSON-encoded array of NotificationEventType ints this notifier is subscribed to.</summary>
    public string SubscribedEventsJson { get; set; } = "[]";

    public string TagsJson { get; set; } = "[]";
}

public sealed class DiscoveryRuleSet
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>JSON: list of typed conditions (Genre IN x, Year &gt;= n, Decade = d, ...).</summary>
    public string ConditionsJson { get; set; } = "[]";

    /// <summary>JSON: action payload (qualityProfileId, rootFolder, tags[], monitorMode).</summary>
    public string ActionJson { get; set; } = "{}";

    public DateTimeOffset? LastRun { get; set; }
}

/// <summary>
/// Singleton-row application config persisted to SQLite.
/// Naming/MediaManagement/Host config bundles live here so the user can mutate them via REST.
/// </summary>
public sealed class ApplicationConfig
{
    public int Id { get; set; }

    // Host
    public string InstanceName { get; set; } = "Vidarr";
    public string? UrlBase { get; set; }
    public string LogLevel { get; set; } = "Information";

    // Media management
    public string FileOperation { get; set; } = "Move"; // Move | Copy | HardlinkWithFallback
    public bool ReplaceIllegalCharacters { get; set; } = true;
    public char IllegalCharacterReplacement { get; set; } = '_';

    // Naming
    public string ArtistFolderTemplate { get; set; } = "{Artist Name}";
    public string FileTemplate { get; set; } = "{Artist Name} - {Title} ({Year}) [{Quality Full}]";

    // Persisted REST API key. Sonarr/Radarr parity: generated on first boot,
    // displayed + rotatable through the Settings UI. Env var VIDARR_API_KEY and
    // appsettings:Vidarr:ApiKey still override this when set.
    public string? ApiKey { get; set; }

    // yt-dlp updater (Phase 13). Opt-in.
    public bool YtDlpAutoUpdate { get; set; }
    public string YtDlpBinaryPath { get; set; } = "yt-dlp";

    // Auth (Phase 12). Method is "None" (default) or "Forms".
    public string AuthMethod { get; set; } = "None";
    public string? AuthUsername { get; set; }
    public string? AuthPasswordHash { get; set; }
    /// <summary>Base64-encoded random secret used to sign session cookies. Created on first use.</summary>
    public string? SessionSecret { get; set; }

    public DateTimeOffset Updated { get; set; }
}
