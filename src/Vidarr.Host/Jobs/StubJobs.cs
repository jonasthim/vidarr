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

/// <summary>Stubbed in P7; the discovery-rule engine lands in Phase 9.</summary>
[ExcludeFromCodeCoverage(Justification = "Stub job — engine in Phase 9.")]
public sealed class RuleSetEvaluationJob : IRecurringJob
{
    private readonly ILogger<RuleSetEvaluationJob> _logger;
    public RuleSetEvaluationJob(ILogger<RuleSetEvaluationJob> logger) { _logger = logger; }
    public string Name => "RuleSetEvaluation";
    public TimeSpan Interval => TimeSpan.FromDays(1);
    public Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("RuleSetEvaluation: deferred to Phase 9 (engine not yet wired)");
        return Task.CompletedTask;
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

/// <summary>Stubbed in P7; the health checks land in Phase 12.</summary>
[ExcludeFromCodeCoverage(Justification = "Stub job — engine in Phase 12.")]
public sealed class HealthCheckJob : IRecurringJob
{
    private readonly ILogger<HealthCheckJob> _logger;
    public HealthCheckJob(ILogger<HealthCheckJob> logger) { _logger = logger; }
    public string Name => "HealthCheck";
    public TimeSpan Interval => TimeSpan.FromMinutes(15);
    public Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("HealthCheck: deferred to Phase 12 (checks not yet wired)");
        return Task.CompletedTask;
    }
}
