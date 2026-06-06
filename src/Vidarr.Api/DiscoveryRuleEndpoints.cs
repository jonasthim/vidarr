using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.Rules;

namespace Vidarr.Api;

public static class DiscoveryRuleEndpoints
{
    public static IEndpointRouteBuilder MapVidarrDiscoveryRuleApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        // Ad-hoc evaluation of a single rule.
        v1.MapPost("/discoveryrule/evaluate/{id:int}", async (int id, IDiscoveryRuleEngine engine, CancellationToken ct) =>
        {
            var result = await engine.EvaluateAsync(id, ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(new DiscoveryEvaluationDto(result.RuleId, result.RuleName, result.Matched, result.VideosMonitored));
        });

        // Evaluate all enabled rules.
        v1.MapPost("/discoveryrule/evaluate-all", async (IDiscoveryRuleEngine engine, CancellationToken ct) =>
        {
            var results = await engine.EvaluateAllAsync(ct);
            return Results.Ok(results.Select(r =>
                new DiscoveryEvaluationDto(r.RuleId, r.RuleName, r.Matched, r.VideosMonitored)).ToArray());
        });

        return app;
    }
}

[ExcludeFromCodeCoverage(Justification = "Plain transport DTO.")]
public sealed record DiscoveryEvaluationDto(int RuleId, string RuleName, int Matched, int VideosMonitored);
