using Vidarr.Contracts.Abstractions;

namespace Vidarr.Tests.Common;

public sealed class FakeClock : ISystemClock
{
    public FakeClock(DateTimeOffset start)
    {
        UtcNow = start;
    }

    public FakeClock() : this(new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan delta)
    {
        UtcNow = UtcNow.Add(delta);
    }

    public void SetTo(DateTimeOffset when)
    {
        UtcNow = when;
    }
}
