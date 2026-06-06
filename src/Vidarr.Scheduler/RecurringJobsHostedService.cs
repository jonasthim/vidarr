using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Vidarr.Scheduler;

[ExcludeFromCodeCoverage(Justification = "BackgroundService; tick loop covered by tests against TickInternal indirectly.")]
public sealed class RecurringJobsHostedService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<RecurringJobsHostedService> _logger;
    private readonly Dictionary<string, DateTimeOffset> _nextRunAt = new(StringComparer.OrdinalIgnoreCase);

    public RecurringJobsHostedService(IServiceProvider services, ILogger<RecurringJobsHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Recurring job tick failed");
            }
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task TickOnceAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IRecurringJob>().ToList();
        var runner = scope.ServiceProvider.GetRequiredService<IRecurringJobRunner>();
        var clock = scope.ServiceProvider.GetRequiredService<Vidarr.Contracts.Abstractions.ISystemClock>();
        var now = clock.UtcNow;

        foreach (var job in jobs)
        {
            if (!_nextRunAt.TryGetValue(job.Name, out var when))
            {
                // first tick after start: schedule the job 30s out so the worker doesn't pile up on boot
                _nextRunAt[job.Name] = now.Add(TimeSpan.FromSeconds(30));
                continue;
            }
            if (now < when) continue;

            _ = Task.Run(async () =>
            {
                try
                {
                    await runner.RunByNameAsync(job.Name, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Job {Job} threw outside the runner safety net", job.Name);
                }
            }, ct);

            _nextRunAt[job.Name] = now.Add(job.Interval);
        }
    }
}
