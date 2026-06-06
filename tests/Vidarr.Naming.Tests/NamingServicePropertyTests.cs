using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Vidarr.Contracts.Models;
using Vidarr.Naming;

namespace Vidarr.Naming.Tests;

public class NamingServicePropertyTests
{
    private readonly INamingService _sut = new NamingService();

    [Property]
    public Property Renderer_introduces_no_braces(NonEmptyString artist, NonEmptyString title, int year)
    {
        // Property is about the renderer, not the inputs — skip cases where inputs already contain braces.
        if (artist.Get.AsSpan().IndexOfAny('{', '}') >= 0 || title.Get.AsSpan().IndexOfAny('{', '}') >= 0)
        {
            return true.ToProperty();
        }
        var input = new NamingInput(artist.Get, title.Get, year, Quality.Webdl1080p, "mkv");
        var path = _sut.BuildRelativePath(input, NamingConfig.Default);
        return (!path.Contains('{') && !path.Contains('}')).ToProperty();
    }

    [Property]
    public Property Output_is_deterministic(NonEmptyString artist, NonEmptyString title)
    {
        var input = new NamingInput(artist.Get, title.Get, 2020, Quality.Webdl1080p, "mkv");
        var a = _sut.BuildRelativePath(input, NamingConfig.Default);
        var b = _sut.BuildRelativePath(input, NamingConfig.Default);
        return (a == b).ToProperty();
    }

    [Property]
    public Property Output_is_never_empty_for_nonempty_inputs(NonEmptyString artist, NonEmptyString title)
    {
        var input = new NamingInput(artist.Get, title.Get, 2020, Quality.Webdl1080p, "mkv");
        var path = _sut.BuildRelativePath(input, NamingConfig.Default);
        return (!string.IsNullOrWhiteSpace(path)).ToProperty();
    }

    [Property]
    public Property Filename_segment_contains_no_invalid_chars_when_sanitised(NonEmptyString artist, NonEmptyString title)
    {
        var input = new NamingInput(artist.Get, title.Get, 2020, Quality.Webdl1080p, "mkv");
        var path = _sut.BuildRelativePath(input, NamingConfig.Default);
        var fileName = Path.GetFileName(path);
        var invalid = Path.GetInvalidFileNameChars();
        return (!fileName.Any(c => invalid.Contains(c))).ToProperty();
    }

    [Property]
    public Property Output_always_ends_with_supplied_extension(NonEmptyString artist, NonEmptyString title, NonEmptyString ext)
    {
        var rawExt = ext.Get.Trim().Trim('.');
        if (string.IsNullOrEmpty(rawExt))
        {
            return true.ToProperty();
        }
        if (rawExt.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
        {
            return true.ToProperty();
        }

        var input = new NamingInput(artist.Get, title.Get, 2020, Quality.Webdl1080p, rawExt);
        var path = _sut.BuildRelativePath(input, NamingConfig.Default);
        return path.EndsWith("." + rawExt, StringComparison.Ordinal).ToProperty();
    }
}
