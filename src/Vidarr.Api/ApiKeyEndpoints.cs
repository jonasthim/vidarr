using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Vidarr.Api;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapVidarrApiKeyApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/system/apikey");

        v1.MapGet("", async (IApiKeyService svc, CancellationToken ct) =>
            Results.Ok(new ApiKeyDto(await svc.GetCurrentAsync(ct))));

        v1.MapPost("/rotate", async (IApiKeyService svc, CancellationToken ct) =>
        {
            try
            {
                var next = await svc.RotateAsync(ct);
                return Results.Ok(new ApiKeyDto(next));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("apiKey", ex.Message)]));
            }
        });

        return app;
    }
}

public sealed record ApiKeyDto(string ApiKey);
