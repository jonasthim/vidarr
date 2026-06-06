using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.Scheduler;

namespace Vidarr.Api;

public static class SystemCommandEndpoints
{
    public static IEndpointRouteBuilder MapVidarrSystemCommandApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/system/command", (IRecurringJobRunner runner, IJobRunHistory history) =>
        {
            var jobs = runner.ListJobs();
            return Results.Ok(jobs.Select(j => new SystemCommandDto(
                Name: j.Name,
                IntervalSeconds: (int)j.Interval.TotalSeconds,
                LastRun: j.LastRun,
                LastRunOk: j.LastRun is not null,
                Recent: history.Recent(j.Name, take: 5)
                    .Select(r => new SystemCommandRunDto(r.StartedAt, r.FinishedAt, r.Succeeded, r.FailureReason))
                    .ToArray())).ToArray());
        });

        v1.MapPost("/system/command/{name}", async (string name, IRecurringJobRunner runner, CancellationToken ct) =>
        {
            // Fire-and-forget: enqueue on the thread-pool so the HTTP handler returns promptly.
            _ = Task.Run(async () =>
            {
                try { await runner.RunByNameAsync(name, ct); }
                catch { /* runner already records failure into history */ }
            }, CancellationToken.None);
            return Results.Accepted(value: new SystemCommandTriggerResponse("queued", name));
        });

        v1.MapGet("/system/jobs/runs", (IJobRunHistory history, string? job, int? take) =>
            Results.Ok(history.Recent(job, take ?? 50).Select(r =>
                new SystemCommandRunDto(r.StartedAt, r.FinishedAt, r.Succeeded, r.FailureReason)).ToArray()));

        return app;
    }
}

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record SystemCommandDto(string Name, int IntervalSeconds, DateTimeOffset? LastRun, bool LastRunOk, IReadOnlyList<SystemCommandRunDto> Recent);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record SystemCommandRunDto(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, bool Succeeded, string? FailureReason);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record SystemCommandTriggerResponse(string Status, string Name);
