namespace Vidarr.Catalog.Entities;

public enum HistoryEventType
{
    Grabbed = 1,
    Imported = 2,
    Upgraded = 3,
    Failed = 4,
    Deleted = 5,
    Renamed = 6,
    Ignored = 7,
}

public sealed class HistoryEvent
{
    public int Id { get; set; }
    public HistoryEventType EventType { get; set; }
    public DateTimeOffset Date { get; set; }

    public int? ArtistId { get; set; }
    public int? MusicVideoId { get; set; }

    public string? ReleaseTitle { get; set; }
    public string? IndexerName { get; set; }
    public string? DownloadClientName { get; set; }
    public int? QualityId { get; set; }

    /// <summary>
    /// Free-form JSON for event-type-specific extras (rejection reasons, file paths, message).
    /// </summary>
    public string DataJson { get; set; } = "{}";
}
