using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.Health;

namespace Vidarr.Api;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapVidarrHealthApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/health", (IHealthMonitor monitor) =>
        {
            var status = monitor.CurrentStatus();
            return Results.Ok(new HealthStatusDto(
                LastRun: status.LastRun,
                Issues: [.. status.Active.Select(i => new HealthIssueDto(
                    i.Id.CheckName, i.Id.Source, i.Severity.ToString(), i.Message))]));
        });

        v1.MapPost("/health/run", async (IHealthMonitor monitor, CancellationToken ct) =>
        {
            var status = await monitor.RunAllAsync(ct);
            return Results.Ok(new HealthStatusDto(
                LastRun: status.LastRun,
                Issues: [.. status.Active.Select(i => new HealthIssueDto(
                    i.Id.CheckName, i.Id.Source, i.Severity.ToString(), i.Message))]));
        });

        return app;
    }
}

public sealed record HealthStatusDto(DateTimeOffset? LastRun, IReadOnlyList<HealthIssueDto> Issues);
public sealed record HealthIssueDto(string CheckName, string Source, string Severity, string Message);
