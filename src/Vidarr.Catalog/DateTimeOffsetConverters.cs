using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Vidarr.Catalog;

/// <summary>
/// SQLite stores DateTimeOffset as TEXT by default, which can't be ORDER BY'd server-side
/// (EF Core throws NotSupportedException). Converting to ticks (long) gives us cheap
/// monotonic ordering and exact round-trips for UTC. Local-offset information is lost,
/// which is fine because Vidarr only ever stores UtcNow timestamps.
/// </summary>
internal sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public static readonly DateTimeOffsetToTicksConverter Instance = new();

    private DateTimeOffsetToTicksConverter()
        : base(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero))
    {
    }
}

internal sealed class NullableDateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset?, long?>
{
    public static readonly NullableDateTimeOffsetToTicksConverter Instance = new();

    private NullableDateTimeOffsetToTicksConverter()
        : base(
            v => v == null ? null : v.Value.UtcTicks,
            v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero))
    {
    }
}
