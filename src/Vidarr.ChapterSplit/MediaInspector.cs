using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.ChapterSplit;

public sealed record MediaInfo(
    TimeSpan Duration,
    IReadOnlyList<MediaChapter> Chapters,
    IReadOnlyList<MediaStream> Streams,
    string? Container);

public sealed record MediaChapter(int Id, TimeSpan Start, TimeSpan End, string? Title)
{
    public TimeSpan Duration => End - Start;
}

public sealed record MediaStream(int Index, string CodecType, string? CodecName, string? Language);

public interface IMediaInspector
{
    Task<MediaInfo?> InspectAsync(string filePath, CancellationToken ct);
}

public sealed class MediaInspector : IMediaInspector
{
    private const string FfprobeExecutable = "ffprobe";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IProcessRunner _processes;
    private readonly TimeSpan _timeout;

    public MediaInspector(IProcessRunner processes, TimeSpan? timeout = null)
    {
        _processes = processes;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<MediaInfo?> InspectAsync(string filePath, CancellationToken ct)
    {
        var invocation = new ProcessInvocation(FfprobeExecutable,
        [
            "-hide_banner",
            "-loglevel", "error",
            "-show_chapters",
            "-show_streams",
            "-show_format",
            "-of", "json",
            filePath,
        ],
            Timeout: _timeout);

        var result = await _processes.RunAsync(invocation, ct);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return null;
        }

        return Parse(result.StdOut);
    }

    public static MediaInfo? Parse(string ffprobeJson)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<FfprobeDoc>(ffprobeJson, JsonOpts);
            if (doc is null) return null;

            var duration = ParseSeconds(doc.Format?.Duration) ?? TimeSpan.Zero;
            var container = doc.Format?.FormatName;

            var chapters = (doc.Chapters ?? [])
                .Select(c => new MediaChapter(
                    Id: c.Id,
                    Start: ParseSeconds(c.StartTime) ?? TimeSpan.Zero,
                    End: ParseSeconds(c.EndTime) ?? TimeSpan.Zero,
                    Title: c.Tags?.GetValueOrDefault("title")))
                .Where(c => c.End > c.Start)
                .ToList();

            var streams = (doc.Streams ?? [])
                .Select(s => new MediaStream(
                    Index: s.Index,
                    CodecType: s.CodecType ?? "unknown",
                    CodecName: s.CodecName,
                    Language: s.Tags?.GetValueOrDefault("language")))
                .ToList();

            return new MediaInfo(duration, chapters, streams, container);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TimeSpan? ParseSeconds(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)) return null;
        if (double.IsNaN(sec) || double.IsInfinity(sec) || sec < 0) return null;
        return TimeSpan.FromSeconds(sec);
    }

    private sealed record FfprobeDoc(List<FfprobeChapter>? Chapters, List<FfprobeStream>? Streams, FfprobeFormat? Format);

    private sealed record FfprobeChapter(
        int Id,
        [property: JsonPropertyName("time_base")] string? TimeBase,
        [property: JsonPropertyName("start_time")] string? StartTime,
        [property: JsonPropertyName("end_time")] string? EndTime,
        Dictionary<string, string>? Tags);

    private sealed record FfprobeStream(
        int Index,
        [property: JsonPropertyName("codec_type")] string? CodecType,
        [property: JsonPropertyName("codec_name")] string? CodecName,
        Dictionary<string, string>? Tags);

    private sealed record FfprobeFormat(
        [property: JsonPropertyName("format_name")] string? FormatName,
        string? Duration);
}
