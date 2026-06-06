using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Scheduler;

public interface IRecurringJob
{
    string Name { get; }
    TimeSpan Interval { get; }
    Task RunAsync(CancellationToken ct);
}

public sealed record JobRun(
    string JobName,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    bool Succeeded,
    string? FailureReason);

public interface IJobRunHistory
{
    void RecordStart(string job, DateTimeOffset at);
    void RecordEnd(string job, DateTimeOffset at, bool succeeded, string? reason);
    IReadOnlyList<JobRun> Recent(string? job = null, int take = 50);
    DateTimeOffset? LastSuccessful(string job);
}

public sealed class InMemoryJobRunHistory : IJobRunHistory
{
    private readonly object _lock = new();
    private readonly LinkedList<JobRun> _runs = new();
    private const int Capacity = 500;

    public void RecordStart(string job, DateTimeOffset at)
    {
        lock (_lock)
        {
            _runs.AddFirst(new JobRun(job, at, null, false, null));
            while (_runs.Count > Capacity) _runs.RemoveLast();
        }
    }

    public void RecordEnd(string job, DateTimeOffset at, bool succeeded, string? reason)
    {
        lock (_lock)
        {
            // Patch the most recent open run for this job (RecordStart adds at head; we walk forward).
            for (var node = _runs.First; node is not null; node = node.Next)
            {
                if (node.Value.JobName == job && node.Value.FinishedAt is null)
                {
                    node.Value = node.Value with { FinishedAt = at, Succeeded = succeeded, FailureReason = reason };
                    return;
                }
            }
            // No open start (e.g. the start was evicted) — record a synthetic end-only row.
            _runs.AddFirst(new JobRun(job, at, at, succeeded, reason));
            while (_runs.Count > Capacity) _runs.RemoveLast();
        }
    }

    public IReadOnlyList<JobRun> Recent(string? job = null, int take = 50)
    {
        lock (_lock)
        {
            return [.. _runs.Where(r => job is null || r.JobName == job).Take(take)];
        }
    }

    public DateTimeOffset? LastSuccessful(string job)
    {
        lock (_lock)
        {
            return _runs.Where(r => r.JobName == job && r.Succeeded && r.FinishedAt is not null)
                .Select(r => r.FinishedAt)
                .FirstOrDefault();
        }
    }
}

public interface IRecurringJobRunner
{
    Task RunByNameAsync(string jobName, CancellationToken ct);
    IReadOnlyList<RecurringJobDescriptor> ListJobs();
}

public sealed record RecurringJobDescriptor(string Name, TimeSpan Interval, DateTimeOffset? LastRun);

public sealed class RecurringJobRunner : IRecurringJobRunner
{
    private readonly IEnumerable<IRecurringJob> _jobs;
    private readonly IJobRunHistory _history;
    private readonly ISystemClock _clock;
    private readonly ILogger<RecurringJobRunner> _logger;

    public RecurringJobRunner(
        IEnumerable<IRecurringJob> jobs,
        IJobRunHistory history,
        ISystemClock clock,
        ILogger<RecurringJobRunner> logger)
    {
        _jobs = jobs;
        _history = history;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunByNameAsync(string jobName, CancellationToken ct)
    {
        var job = _jobs.FirstOrDefault(j => string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));
        if (job is null)
        {
            _logger.LogWarning("RunByName: unknown job {Job}", jobName);
            return;
        }

        _history.RecordStart(job.Name, _clock.UtcNow);
        try
        {
            await job.RunAsync(ct);
            _history.RecordEnd(job.Name, _clock.UtcNow, true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _history.RecordEnd(job.Name, _clock.UtcNow, false, ex.Message);
            _logger.LogError(ex, "Job {Job} failed", job.Name);
        }
    }

    public IReadOnlyList<RecurringJobDescriptor> ListJobs() =>
        [.. _jobs.Select(j => new RecurringJobDescriptor(j.Name, j.Interval, _history.LastSuccessful(j.Name)))];
}
