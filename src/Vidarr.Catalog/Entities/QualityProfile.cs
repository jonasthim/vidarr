namespace Vidarr.Catalog.Entities;

public sealed class QualityProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CutoffQualityId { get; set; }
    public bool UpgradeAllowed { get; set; } = true;
    public int MinFormatScore { get; set; }
    public long? MinSizeBytes { get; set; }
    public long? MaxSizeBytes { get; set; }

    /// <summary>
    /// JSON array of ints (Quality.Id), ordered worst→best, that are allowed by this profile.
    /// Stored as JSON for SQLite portability — same shape Sonarr uses.
    /// </summary>
    public string AllowedQualityIdsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of { customFormatId, score } — additive scoring applied during decision.
    /// Format engine is stubbed in Phase 1 and lands fully in Phase 8.
    /// </summary>
    public string FormatItemsJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of Tag IDs scoping this profile.
    /// </summary>
    public string TagsJson { get; set; } = "[]";
}
