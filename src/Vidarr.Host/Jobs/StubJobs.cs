using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Vidarr.Scheduler;

namespace Vidarr.Host.Jobs;

/// <summary>Stubbed in P7; the wanted-video search lands fully in Phase 8 alongside the Custom Format engine.</summary>
[ExcludeFromCodeCoverage(Justification = "Stub job — engine in Phase 8.")]
public sealed class WantedVideoSearchJob : IRecurringJob
{
    private readonly ILogger<WantedVideoSearchJob> _logger;
    public WantedVideoSearchJob(ILogger<WantedVideoSearchJob> logger) { _logger = logger; }
    public string Name => "WantedVideoSearch";
    public TimeSpan Interval => TimeSpan.FromDays(1);
    public Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("WantedVideoSearch: deferred to Phase 8 (engine not yet wired)");
        return Task.CompletedTask;
    }
}

/// <summary>Nightly evaluation of <see cref="Vidarr.Rules.IDiscoveryRuleEngine"/>.</summary>
[ExcludeFromCodeCoverage(Justification = "Composition job; integration-tested via the runner.")]
public sealed class RuleSetEvaluationJob : IRecurringJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RuleSetEvaluationJob> _logger;
    public RuleSetEvaluationJob(IServiceProvider services, ILogger<RuleSetEvaluationJob> logger)
    {
        _services = services;
        _logger = logger;
    }
    public string Name => "RuleSetEvaluation";
    public TimeSpan Interval => TimeSpan.FromDays(1);

    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<Vidarr.Rules.IDiscoveryRuleEngine>();
        var results = await engine.EvaluateAllAsync(ct);
        foreach (var r in results)
        {
            _logger.LogInformation("RuleSetEvaluation {Rule}: matched={Matched}, monitored={Monitored}",
                r.RuleName, r.Matched, r.VideosMonitored);
        }
    }
}

/// <summary>Stubbed in P7; the backup pipeline lands in Phase 13.</summary>
[ExcludeFromCodeCoverage(Justification = "Stub job — engine in Phase 13.")]
public sealed class BackupJob : IRecurringJob
{
    private readonly ILogger<BackupJob> _logger;
    public BackupJob(ILogger<BackupJob> logger) { _logger = logger; }
    public string Name => "Backup";
    public TimeSpan Interval => TimeSpan.FromDays(7);
    public Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Backup: deferred to Phase 13 (engine not yet wired)");
        return Task.CompletedTask;
    }
}

public sealed class HealthCheckJob : IRecurringJob
{
    private readonly Vidarr.Health.IHealthMonitor _monitor;
    private readonly ILogger<HealthCheckJob> _logger;
    public HealthCheckJob(Vidarr.Health.IHealthMonitor monitor, ILogger<HealthCheckJob> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }
    public string Name => "HealthCheck";
    public TimeSpan Interval => TimeSpan.FromMinutes(15);
    public async Task RunAsync(CancellationToken ct)
    {
        var status = await _monitor.RunAllAsync(ct);
        _logger.LogInformation("HealthCheck: {Count} active issue(s)", status.Active.Count);
    }
}
