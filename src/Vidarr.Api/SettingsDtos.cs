using System.Diagnostics.CodeAnalysis;
using Vidarr.Catalog.Entities;

namespace Vidarr.Api;

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record TagDto(int Id, string Label);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record TagRequest(string Label);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record QualityProfileDto(
    int Id,
    string Name,
    IReadOnlyList<int> AllowedQualityIds,
    int CutoffQualityId,
    bool UpgradeAllowed,
    int MinFormatScore,
    long? MinSizeBytes,
    long? MaxSizeBytes,
    IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record QualityProfileRequest(
    string Name,
    IReadOnlyList<int> AllowedQualityIds,
    int CutoffQualityId,
    bool UpgradeAllowed,
    int MinFormatScore,
    long? MinSizeBytes,
    long? MaxSizeBytes,
    IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record CustomFormatDto(int Id, string Name, bool IncludeCustomFormatWhenRenaming, string SpecificationsJson);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record CustomFormatRequest(string Name, bool IncludeCustomFormatWhenRenaming, string SpecificationsJson);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record BlocklistDto(int Id, int? ArtistId, int? MusicVideoId, string ReleaseTitle, string IndexerName, string? Reason, DateTimeOffset Date);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record BlocklistRequest(int? ArtistId, int? MusicVideoId, string ReleaseTitle, string IndexerName, string? Reason);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record HistoryDto(int Id, string EventType, DateTimeOffset Date, int? ArtistId, int? MusicVideoId,
    string? ReleaseTitle, string? IndexerName, string? DownloadClientName, int? QualityId, string DataJson);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record IndexerConfigDto(int Id, string Name, string Implementation, string SettingsJson,
    int Priority, bool EnableRss, bool EnableAutomaticSearch, bool EnableInteractiveSearch,
    int? PreferredDownloadClientId, IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record IndexerConfigRequest(string Name, string Implementation, string SettingsJson,
    int Priority, bool EnableRss, bool EnableAutomaticSearch, bool EnableInteractiveSearch,
    int? PreferredDownloadClientId, IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DownloadClientConfigDto(int Id, string Name, string Implementation, string SettingsJson,
    int Priority, bool Enable, string? Category, bool RemovesCompletedDownloads, IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DownloadClientConfigRequest(string Name, string Implementation, string SettingsJson,
    int Priority, bool Enable, string? Category, bool RemovesCompletedDownloads, IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record NotificationConfigDto(int Id, string Name, string Implementation, string SettingsJson,
    bool Enable, IReadOnlyList<int> SubscribedEvents, IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record NotificationConfigRequest(string Name, string Implementation, string SettingsJson,
    bool Enable, IReadOnlyList<int> SubscribedEvents, IReadOnlyList<int> Tags);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DiscoveryRuleSetDto(int Id, string Name, bool Enabled, string ConditionsJson, string ActionJson, DateTimeOffset? LastRun);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DiscoveryRuleSetRequest(string Name, bool Enabled, string ConditionsJson, string ActionJson);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record HostConfigDto(string InstanceName, string? UrlBase, string LogLevel);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record HostConfigRequest(string InstanceName, string? UrlBase, string LogLevel);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record NamingConfigDto(string ArtistFolderTemplate, string FileTemplate);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record MediaManagementConfigDto(string FileOperation, bool ReplaceIllegalCharacters, char IllegalCharacterReplacement);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record RootFolderDto(int Id, string Path, bool Accessible, long FreeBytes, long TotalBytes);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record RootFolderRequest(string Path);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record QualityDto(int Id, string Name, string Resolution, string Source);
