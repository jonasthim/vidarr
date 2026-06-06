using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.Indexers;
using Vidarr.Scheduler;

namespace Vidarr.Host.Jobs;

/// <summary>
/// Periodic RSS sync — calls <see cref="IReleaseSearchService.RssSyncAsync"/> for every
/// indexer that supports RSS. The decision/grab pipeline is wired in Phase 8 (Custom
/// Format engine); for now we count the results and emit a structured log line.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Composition job; integration-tested via the runner.")]
public sealed class RssSyncJob : IRecurringJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RssSyncJob> _logger;

    public RssSyncJob(IServiceProvider services, ILogger<RssSyncJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public string Name => "RssSync";
    public TimeSpan Interval => TimeSpan.FromMinutes(15);

    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IReleaseSearchService>();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var result = await search.RssSyncAsync(ct);
        _logger.LogInformation(
            "RssSync: {Releases} releases / {Failures} failures across {Indexers} indexers",
            result.Releases.Count, result.Failures.Count, result.IndexersQueried);

        foreach (var fail in result.Failures)
        {
            await bus.PublishAsync(new HealthIssueEvent(
                OccurredAt: DateTimeOffset.UtcNow,
                Source: $"Indexer/{fail.IndexerName}",
                Severity: HealthSeverity.Warning,
                Message: fail.Reason,
                Resolved: false), ct);
        }
    }
}
