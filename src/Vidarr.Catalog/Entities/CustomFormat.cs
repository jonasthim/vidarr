namespace Vidarr.Catalog.Entities;

public sealed class CustomFormat
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IncludeCustomFormatWhenRenaming { get; set; }

    /// <summary>
    /// JSON-encoded array of specification descriptors:
    ///   { name, implementation, negate, required, fields: {...} }
    /// Engine evaluation lands in Phase 8 (CustomFormatEngine).
    /// </summary>
    public string SpecificationsJson { get; set; } = "[]";
}
