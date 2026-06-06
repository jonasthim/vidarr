using Vidarr.Contracts.Models;

namespace Vidarr.Contracts.Events;

public sealed record GrabEvent(
    DateTimeOffset OccurredAt,
    int ArtistId,
    IReadOnlyList<int> MusicVideoIds,
    string ReleaseTitle,
    string IndexerName,
    string DownloadClientName,
    Quality Quality);

public sealed record ImportEvent(
    DateTimeOffset OccurredAt,
    int ArtistId,
    int MusicVideoId,
    string FilePath,
    long SizeBytes,
    Quality Quality,
    string? SourceLabel);

public sealed record UpgradeEvent(
    DateTimeOffset OccurredAt,
    int ArtistId,
    int MusicVideoId,
    string FilePath,
    Quality OldQuality,
    Quality NewQuality);

public sealed record DeleteEvent(
    DateTimeOffset OccurredAt,
    int ArtistId,
    int MusicVideoId,
    string FilePath);

public sealed record HealthIssueEvent(
    DateTimeOffset OccurredAt,
    string Source,
    HealthSeverity Severity,
    string Message,
    bool Resolved);

public enum HealthSeverity
{
    Notice = 0,
    Warning = 1,
    Error = 2,
}

public sealed record DownloadCompletedEvent(
    DateTimeOffset OccurredAt,
    int DownloadId,
    string OutputPath);

public sealed record DownloadFailedEvent(
    DateTimeOffset OccurredAt,
    int DownloadId,
    string Reason);

public sealed record ReleaseGrabbedEvent(
    DateTimeOffset OccurredAt,
    int ArtistId,
    IReadOnlyList<int> MusicVideoIds,
    string ReleaseTitle,
    string IndexerName,
    string DownloadClientName,
    Quality Quality);
