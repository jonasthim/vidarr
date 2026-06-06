using System.Globalization;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.ChapterSplit;

public sealed record ChapterSplitRequest(string InputPath, MediaChapter Chapter, string OutputPath);

public sealed record ChapterSplitResult(MediaChapter Chapter, string OutputPath, bool Success, string? FailureReason);

public interface IChapterSplitter
{
    Task<ChapterSplitResult> SplitAsync(ChapterSplitRequest request, CancellationToken ct);
}

public sealed class ChapterSplitter : IChapterSplitter
{
    private const string FfmpegExecutable = "ffmpeg";

    private readonly IProcessRunner _processes;
    private readonly TimeSpan _timeout;

    public ChapterSplitter(IProcessRunner processes, TimeSpan? timeout = null)
    {
        _processes = processes;
        _timeout = timeout ?? TimeSpan.FromMinutes(10);
    }

    public async Task<ChapterSplitResult> SplitAsync(ChapterSplitRequest request, CancellationToken ct)
    {
        if (request.Chapter.Duration <= TimeSpan.Zero)
        {
            return new ChapterSplitResult(request.Chapter, request.OutputPath, false, "Chapter has zero or negative duration");
        }

        var args = BuildArgs(request);
        var invocation = new ProcessInvocation(FfmpegExecutable, args, Timeout: _timeout);
        var result = await _processes.RunAsync(invocation, ct);
        return result.ExitCode == 0
            ? new ChapterSplitResult(request.Chapter, request.OutputPath, true, null)
            : new ChapterSplitResult(request.Chapter, request.OutputPath, false,
                string.IsNullOrEmpty(result.StdErr) ? $"ffmpeg exit code {result.ExitCode}" : result.StdErr.Trim());
    }

    /// <summary>
    /// Builds the argument list for an accurate stream-copy chapter extraction.
    /// -ss placed AFTER -i gives an accurate cut at the cost of more I/O; with -c copy
    /// the cut still snaps to the closest preceding keyframe but ffmpeg will fast-forward
    /// internally to the right starting moment.
    /// </summary>
    public static IReadOnlyList<string> BuildArgs(ChapterSplitRequest request) =>
    [
        "-hide_banner",
        "-loglevel", "error",
        "-y",
        "-i", request.InputPath,
        "-ss", FormatSeconds(request.Chapter.Start),
        "-to", FormatSeconds(request.Chapter.End),
        "-map", "0",
        "-c", "copy",
        "-map_chapters", "-1",
        "-avoid_negative_ts", "make_zero",
        request.OutputPath,
    ];

    public static string FormatSeconds(TimeSpan t) =>
        t.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}
