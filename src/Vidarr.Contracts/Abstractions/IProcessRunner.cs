namespace Vidarr.Contracts.Abstractions;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken ct);

    Task<ProcessResult> RunStreamingAsync(
        ProcessInvocation invocation,
        Func<string, CancellationToken, Task>? onStdoutLine,
        Func<string, CancellationToken, Task>? onStderrLine,
        CancellationToken ct);
}

public sealed record ProcessInvocation(
    string Executable,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    TimeSpan? Timeout = null);

public sealed record ProcessResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    TimeSpan Duration);
