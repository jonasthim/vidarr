namespace Vidarr.Contracts.Models;

public sealed record ArtistSearchResult(
    string ProviderId,
    string Name,
    string? Disambiguation,
    int? FormedYear,
    string? Country,
    Uri? ThumbnailUrl);

public sealed record ArtistDetails(
    string ProviderId,
    string Name,
    string? SortName,
    string? Disambiguation,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Genres,
    string? Country,
    int? YearsActiveStart,
    int? YearsActiveEnd,
    IReadOnlyList<ArtistImage> Images,
    IReadOnlyDictionary<string, string> ExternalIds,
    IReadOnlyList<string> YouTubeChannelIds);

public sealed record ArtistImage(string Kind, Uri Url);

public sealed record MusicVideoDetails(
    string ProviderId,
    string ArtistProviderId,
    string Title,
    IReadOnlyList<string> AlternateTitles,
    int? Year,
    DateOnly? ReleaseDate,
    MusicVideoType Type,
    string? Director,
    string? ProductionCompany,
    TimeSpan? Runtime,
    IReadOnlyList<string> Genres,
    Uri? ThumbnailUrl,
    IReadOnlyDictionary<string, string> ExternalIds);
