using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Infrastructure;

[ExcludeFromCodeCoverage(Justification = "Boundary adapter; covered by integration tests that exercise real subprocesses.")]
public sealed class ProcessRunnerAdapter : IProcessRunner
{
    public Task<ProcessResult> RunAsync(ProcessInvocation invocation, CancellationToken ct) =>
        RunStreamingAsync(invocation, null, null, ct);

    public async Task<ProcessResult> RunStreamingAsync(
        ProcessInvocation invocation,
        Func<string, CancellationToken, Task>? onStdoutLine,
        Func<string, CancellationToken, Task>? onStderrLine,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.Executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = invocation.WorkingDirectory ?? string.Empty,
        };

        foreach (var arg in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (invocation.Environment is not null)
        {
            foreach (var (k, v) in invocation.Environment)
            {
                startInfo.Environment[k] = v;
            }
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        var stdoutBuffer = new System.Text.StringBuilder();
        var stderrBuffer = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }
            stdoutBuffer.AppendLine(e.Data);
            if (onStdoutLine is not null)
            {
                _ = onStdoutLine(e.Data, ct);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }
            stderrBuffer.AppendLine(e.Data);
            if (onStderrLine is not null)
            {
                _ = onStderrLine(e.Data, ct);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (invocation.Timeout is { } timeout)
        {
            cts.CancelAfter(timeout);
        }

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        sw.Stop();
        return new ProcessResult(process.ExitCode, stdoutBuffer.ToString(), stderrBuffer.ToString(), sw.Elapsed);
    }
}
