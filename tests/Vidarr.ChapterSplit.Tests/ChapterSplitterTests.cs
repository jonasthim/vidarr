using Vidarr.ChapterSplit;
using Vidarr.Contracts.Abstractions;
using Vidarr.Tests.Common;

namespace Vidarr.ChapterSplit.Tests;

public class ChapterSplitterTests
{
    private static ChapterSplitRequest SampleRequest(TimeSpan? start = null, TimeSpan? end = null) =>
        new(
            InputPath: "/library/concert.mkv",
            Chapter: new MediaChapter(0, start ?? TimeSpan.FromSeconds(180), end ?? TimeSpan.FromSeconds(360), "Around the World"),
            OutputPath: "/library/out.mkv");

    [Fact]
    public async Task Split_invokes_ffmpeg_with_canonical_argv()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(0, "", "", TimeSpan.Zero));
        var sut = new ChapterSplitter(runner);

        var result = await sut.SplitAsync(SampleRequest(), default);

        result.Success.Should().BeTrue();
        var inv = runner.Invocations.Single();
        inv.Executable.Should().Be("ffmpeg");
        inv.Arguments.Should().Equal(
            "-hide_banner", "-loglevel", "error", "-y",
            "-i", "/library/concert.mkv",
            "-ss", "180",
            "-to", "360",
            "-map", "0",
            "-c", "copy",
            "-map_chapters", "-1",
            "-avoid_negative_ts", "make_zero",
            "/library/out.mkv");
    }

    [Fact]
    public async Task Split_returns_failure_when_ffmpeg_exits_non_zero()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(1, "", "Invalid data", TimeSpan.Zero));
        var sut = new ChapterSplitter(runner);

        var result = await sut.SplitAsync(SampleRequest(), default);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Invalid data");
    }

    [Fact]
    public async Task Split_returns_failure_with_default_message_when_stderr_empty()
    {
        var runner = new FakeProcessRunner().SetDefault(new ProcessResult(2, "", "", TimeSpan.Zero));
        var result = await new ChapterSplitter(runner).SplitAsync(SampleRequest(), default);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("exit code 2");
    }

    [Fact]
    public async Task Split_rejects_zero_or_negative_duration_chapters_without_calling_ffmpeg()
    {
        var runner = new FakeProcessRunner();
        var sut = new ChapterSplitter(runner);

        var result = await sut.SplitAsync(
            SampleRequest(start: TimeSpan.FromSeconds(100), end: TimeSpan.FromSeconds(100)), default);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("zero or negative duration");
        runner.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(180, "180")]
    [InlineData(180.5, "180.5")]
    [InlineData(180.123, "180.123")]
    [InlineData(180.1234, "180.123")]                 // truncated to 3 decimals
    [InlineData(3600.5, "3600.5")]
    public void FormatSeconds_renders_canonical_decimal(double seconds, string expected) =>
        ChapterSplitter.FormatSeconds(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    [Fact]
    public void BuildArgs_is_pure_and_deterministic()
    {
        var args1 = ChapterSplitter.BuildArgs(SampleRequest());
        var args2 = ChapterSplitter.BuildArgs(SampleRequest());
        args1.Should().Equal(args2);
    }
}
