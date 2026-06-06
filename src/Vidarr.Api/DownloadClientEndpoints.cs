using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.DownloadClients;

namespace Vidarr.Api;

public static class DownloadClientEndpoints
{
    public static IEndpointRouteBuilder MapVidarrDownloadClientApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/downloadclient/schema", (IEnumerable<IDownloadClientFactory> factories) =>
            Results.Ok(factories.Select(f => new DownloadClientSchemaDto(
                Implementation: f.Implementation,
                DisplayName: f.DisplayName,
                Protocol: f.Protocol.ToString(),
                Fields: f.SettingsSchema.Select(s =>
                    new DownloadClientSchemaFieldDto(s.Name, s.Label, s.Type, s.Required, s.HelpText)).ToArray())).ToArray()));

        v1.MapPost("/downloadclient/test", async (
            DownloadClientTestRequest req,
            IEnumerable<IDownloadClientFactory> factories,
            CancellationToken ct) =>
        {
            var factory = factories.FirstOrDefault(f =>
                string.Equals(f.Implementation, req.Implementation, StringComparison.OrdinalIgnoreCase));
            if (factory is null)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("implementation", $"Unknown implementation {req.Implementation}")]));
            }
            try
            {
                var client = factory.Create(0, "test", req.SettingsJson ?? "{}");
                var result = await client.TestAsync(ct);
                return Results.Ok(new DownloadClientTestResultDto(result.Success, result.Message));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Ok(new DownloadClientTestResultDto(false, ex.Message));
            }
        });

        return app;
    }
}

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DownloadClientSchemaDto(string Implementation, string DisplayName, string Protocol, IReadOnlyList<DownloadClientSchemaFieldDto> Fields);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DownloadClientSchemaFieldDto(string Name, string Label, string Type, bool Required, string? HelpText);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DownloadClientTestRequest(string Implementation, string? SettingsJson);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record DownloadClientTestResultDto(bool Success, string? Message);
