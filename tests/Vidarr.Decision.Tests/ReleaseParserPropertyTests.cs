using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Vidarr.Decision;

namespace Vidarr.Decision.Tests;

public class ReleaseParserPropertyTests
{
    private readonly IReleaseParser _sut = new ReleaseParser();

    [Property]
    public Property Parser_never_throws_on_arbitrary_input(NonNull<string> input)
    {
        try
        {
            _ = _sut.Parse(input.Get);
            return true.ToProperty();
        }
        catch
        {
            return false.ToProperty();
        }
    }

    [Property]
    public Property Parser_is_deterministic(NonNull<string> input)
    {
        var a = _sut.Parse(input.Get);
        var b = _sut.Parse(input.Get);
        var same =
            a.ArtistName == b.ArtistName
            && a.Title == b.Title
            && a.Year == b.Year
            && a.Quality == b.Quality
            && a.ReleaseGroup == b.ReleaseGroup
            && a.Tags.SequenceEqual(b.Tags);
        return same.ToProperty();
    }

    [Property]
    public Property Year_is_always_in_realistic_range_when_present(NonNull<string> input)
    {
        var parsed = _sut.Parse(input.Get);
        if (parsed.Year is { } y)
        {
            return (y >= 1900 && y <= 2100).ToProperty();
        }
        return true.ToProperty();
    }

    [Property]
    public Property Title_is_never_null(NonNull<string> input)
    {
        var parsed = _sut.Parse(input.Get);
        return (parsed.Title is not null).ToProperty();
    }
}
