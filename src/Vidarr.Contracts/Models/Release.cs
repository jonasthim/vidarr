namespace Vidarr.Contracts.Models;

public sealed record ReleaseInfo(
    string Title,
    Uri SourceUrl,
    string? Magnet,
    long? SizeBytes,
    DateTimeOffset? PublishedAt,
    TimeSpan? Age,
    int? Seeders,
    int? Leechers,
    DownloadProtocol Protocol,
    string IndexerName,
    string? IndexerCategory,
    IReadOnlyDictionary<string, string> ExtraMetadata);

public sealed record IndexerSearchCriteria(
    string Query,
    string? ArtistName,
    string? Title,
    int? Year,
    IReadOnlyList<string> Categories);

public sealed record RemoteRelease(
    ReleaseInfo Info,
    ParsedReleaseInfo Parsed,
    int Score,
    IReadOnlyList<string> RejectionReasons,
    IReadOnlyList<int> MatchedMusicVideoIds);

public sealed record ParsedReleaseInfo(
    string? ArtistName,
    string? Title,
    int? Year,
    Quality Quality,
    string? ReleaseGroup,
    IReadOnlyList<string> Tags);
