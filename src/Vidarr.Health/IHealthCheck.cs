using Vidarr.Contracts.Events;

namespace Vidarr.Health;

public sealed record HealthIssueId(string CheckName, string Source)
{
    public override string ToString() => $"{CheckName}:{Source}";
}

public sealed record HealthIssue(HealthIssueId Id, HealthSeverity Severity, string Message);

public interface IHealthCheck
{
    string Name { get; }
    Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct);
}
