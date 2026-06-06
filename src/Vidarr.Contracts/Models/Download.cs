namespace Vidarr.Contracts.Models;

public readonly record struct DownloadClientItemId(string Value)
{
    public override string ToString() => Value;
}

public enum DownloadItemStatus
{
    Queued = 0,
    Downloading = 1,
    CompletedReadyToImport = 2,
    Importing = 3,
    Imported = 4,
    Failed = 5,
    Removed = 6,
}

public sealed record DownloadClientItem(
    DownloadClientItemId Id,
    string Title,
    long? TotalBytes,
    long? RemainingBytes,
    DownloadItemStatus Status,
    string? OutputPath,
    TimeSpan? Eta,
    string? Message);
