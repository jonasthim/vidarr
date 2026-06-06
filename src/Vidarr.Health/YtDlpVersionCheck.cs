using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;

namespace Vidarr.Health;

public sealed class YtDlpVersionCheck : IHealthCheck
{
    private readonly IProcessRunner _processes;

    public YtDlpVersionCheck(IProcessRunner processes)
    {
        _processes = processes;
    }

    public string Name => nameof(YtDlpVersionCheck);

    public async Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct)
    {
        try
        {
            var result = await _processes.RunAsync(
                new ProcessInvocation("yt-dlp", ["--version"], Timeout: TimeSpan.FromSeconds(10)),
                ct);
            if (result.ExitCode != 0)
            {
                return [new HealthIssue(
                    new HealthIssueId(Name, "yt-dlp"),
                    HealthSeverity.Error,
                    $"yt-dlp exited {result.ExitCode}: {(string.IsNullOrEmpty(result.StdErr) ? "no stderr" : result.StdErr.Trim())}")];
            }
            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [new HealthIssue(
                new HealthIssueId(Name, "yt-dlp"),
                HealthSeverity.Error,
                $"yt-dlp not invocable: {ex.Message}")];
        }
    }
}
