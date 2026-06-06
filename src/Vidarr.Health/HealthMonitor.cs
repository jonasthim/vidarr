using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;

namespace Vidarr.Health;

public sealed record HealthStatus(IReadOnlyList<HealthIssue> Active, DateTimeOffset? LastRun);

public interface IHealthMonitor
{
    Task<HealthStatus> RunAllAsync(CancellationToken ct);
    HealthStatus CurrentStatus();
}

public sealed class HealthMonitor : IHealthMonitor
{
    private readonly IEnumerable<IHealthCheck> _checks;
    private readonly IEventBus _bus;
    private readonly ILogger<HealthMonitor> _logger;

    private readonly ConcurrentDictionary<HealthIssueId, HealthIssue> _active = new();
    private DateTimeOffset? _lastRun;

    public HealthMonitor(IEnumerable<IHealthCheck> checks, IEventBus bus, ILogger<HealthMonitor> logger)
    {
        _checks = checks;
        _bus = bus;
        _logger = logger;
    }

    public async Task<HealthStatus> RunAllAsync(CancellationToken ct)
    {
        var newIssuesByCheck = new Dictionary<string, HashSet<HealthIssueId>>();
        var freshIssues = new Dictionary<HealthIssueId, HealthIssue>();

        foreach (var check in _checks)
        {
            try
            {
                var checkIssues = await check.RunAsync(ct);
                newIssuesByCheck[check.Name] = [.. checkIssues.Select(i => i.Id)];
                foreach (var issue in checkIssues)
                {
                    freshIssues[issue.Id] = issue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var sentinelId = new HealthIssueId(check.Name, "(check itself)");
                newIssuesByCheck[check.Name] = [sentinelId];
                freshIssues[sentinelId] = new HealthIssue(
                    sentinelId, HealthSeverity.Error,
                    $"{check.Name} threw: {ex.Message}");
            }
        }

        // Raise new or changed issues.
        foreach (var (id, issue) in freshIssues)
        {
            if (_active.TryGetValue(id, out var existing)
                && existing.Severity == issue.Severity
                && existing.Message == issue.Message)
            {
                continue;
            }
            _active[id] = issue;
            await _bus.PublishAsync(new HealthIssueEvent(
                OccurredAt: DateTimeOffset.UtcNow,
                Source: id.ToString(),
                Severity: issue.Severity,
                Message: issue.Message,
                Resolved: false), ct);
            _logger.LogInformation("Health issue raised: {Id} ({Severity}) — {Message}",
                id, issue.Severity, issue.Message);
        }

        // Resolve issues that the latest run for the same check did NOT re-surface.
        var coveredChecks = newIssuesByCheck.Keys.ToHashSet(StringComparer.Ordinal);
        var resolvedIds = _active.Keys
            .Where(id => coveredChecks.Contains(id.CheckName)
                && !newIssuesByCheck[id.CheckName].Contains(id))
            .ToList();
        foreach (var id in resolvedIds)
        {
            if (!_active.TryRemove(id, out var existing)) continue;
            await _bus.PublishAsync(new HealthIssueEvent(
                OccurredAt: DateTimeOffset.UtcNow,
                Source: id.ToString(),
                Severity: existing.Severity,
                Message: existing.Message,
                Resolved: true), ct);
            _logger.LogInformation("Health issue resolved: {Id}", id);
        }

        _lastRun = DateTimeOffset.UtcNow;
        return CurrentStatus();
    }

    public HealthStatus CurrentStatus() => new([.. _active.Values], _lastRun);
}
