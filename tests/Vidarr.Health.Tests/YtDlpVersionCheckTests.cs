using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;
using Vidarr.Tests.Common;

namespace Vidarr.Health.Tests;

public class YtDlpVersionCheckTests
{
    [Fact]
    public async Task Returns_no_issues_when_yt_dlp_responds()
    {
        var runner = new FakeProcessRunner();
        runner.WhenExecutable("yt-dlp", new ProcessResult(0, "2026.05.01", string.Empty, TimeSpan.Zero));

        var check = new YtDlpVersionCheck(runner);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Raises_error_when_yt_dlp_exits_nonzero()
    {
        var runner = new FakeProcessRunner();
        runner.WhenExecutable("yt-dlp", new ProcessResult(127, string.Empty, "command not found", TimeSpan.Zero));

        var check = new YtDlpVersionCheck(runner);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Error);
        issues[0].Message.Should().Contain("command not found");
    }

    [Fact]
    public async Task Raises_error_when_invocation_throws()
    {
        var runner = new ThrowingProcessRunner(new InvalidOperationException("binary missing"));
        var check = new YtDlpVersionCheck(runner);

        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Message.Should().Contain("binary missing");
    }

    [Fact]
    public async Task Empty_stderr_message_renders_no_stderr()
    {
        var runner = new FakeProcessRunner();
        runner.WhenExecutable("yt-dlp", new ProcessResult(1, string.Empty, string.Empty, TimeSpan.Zero));
        var check = new YtDlpVersionCheck(runner);

        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle().Which.Message.Should().Contain("no stderr");
    }

    [Fact]
    public void Name_returns_class_name()
    {
        var check = new YtDlpVersionCheck(new FakeProcessRunner());
        check.Name.Should().Be("YtDlpVersionCheck");
    }

    private sealed class ThrowingProcessRunner : IProcessRunner
    {
        private readonly Exception _ex;
        public ThrowingProcessRunner(Exception ex) { _ex = ex; }
        public Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken ct) =>
            Task.FromException<ProcessResult>(_ex);
        public Task<ProcessResult> RunStreamingAsync(
            ProcessInvocation invocation,
            Func<string, CancellationToken, Task>? onStdoutLine,
            Func<string, CancellationToken, Task>? onStderrLine,
            CancellationToken ct) => Task.FromException<ProcessResult>(_ex);
    }
}
