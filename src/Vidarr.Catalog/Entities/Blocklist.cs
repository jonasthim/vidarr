namespace Vidarr.Catalog.Entities;

public sealed class BlocklistEntry
{
    public int Id { get; set; }
    public int? ArtistId { get; set; }
    public int? MusicVideoId { get; set; }
    public string ReleaseTitle { get; set; } = string.Empty;
    public string IndexerName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTimeOffset Date { get; set; }
}
