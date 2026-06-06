using Vidarr.Contracts.Models;

namespace Vidarr.Catalog.Entities;

public sealed class Artist
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SortName { get; set; } = string.Empty;
    public string? Disambiguation { get; set; }
    public string? Country { get; set; }

    public string ExternalIdsJson { get; set; } = "{}";
    public string GenresJson { get; set; } = "[]";
    public string AliasesJson { get; set; } = "[]";
    public string ImagesJson { get; set; } = "[]";
    public string YouTubeChannelIdsJson { get; set; } = "[]";

    public bool Monitored { get; set; }
    public MonitorMode MonitorMode { get; set; } = MonitorMode.All;
    public int QualityProfileId { get; set; }
    public string RootFolderPath { get; set; } = string.Empty;

    public DateTimeOffset Added { get; set; }
    public DateTimeOffset? LastInfoSync { get; set; }
    public DateTimeOffset? LastSearch { get; set; }

    public ICollection<MusicVideo> MusicVideos { get; set; } = [];
}

public sealed class MusicVideo
{
    public int Id { get; set; }
    public int ArtistId { get; set; }
    public Artist? Artist { get; set; }

    public string Title { get; set; } = string.Empty;
    public string AlternateTitlesJson { get; set; } = "[]";
    public int? Year { get; set; }
    public DateTimeOffset? ReleaseDate { get; set; }
    public MusicVideoType Type { get; set; } = MusicVideoType.Official;
    public string? Director { get; set; }
    public string? ProductionCompany { get; set; }
    public TimeSpan? Runtime { get; set; }
    public string GenresJson { get; set; } = "[]";
    public string? ThumbnailUrl { get; set; }
    public string ExternalIdsJson { get; set; } = "{}";

    public bool Monitored { get; set; }
    public bool HasFile { get; set; }
    public int? FileId { get; set; }
    public MusicVideoFile? File { get; set; }
    public DateTimeOffset? LastSearch { get; set; }
}

public sealed class MusicVideoFile
{
    public int Id { get; set; }
    public int MusicVideoId { get; set; }
    public MusicVideo? MusicVideo { get; set; }

    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset DateAdded { get; set; }

    public int QualityId { get; set; }
    public string? MediaInfoJson { get; set; }

    public string? SourceLabel { get; set; }
    public string? IndexerName { get; set; }
    public string? ReleaseTitle { get; set; }
}

public sealed class RootFolder
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool Accessible { get; set; }
    public long FreeBytes { get; set; }
    public long TotalBytes { get; set; }
}
