using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Vidarr.Api;

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapVidarrApiKeyApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/system/apikey");

        v1.MapGet("", async (HttpContext ctx, IApiKeyService svc, CancellationToken ct) =>
        {
            NoStore(ctx);
            return Results.Ok(new ApiKeyDto(await svc.GetCurrentAsync(ct)));
        });

        v1.MapPost("/rotate", async (HttpContext ctx, IApiKeyService svc, CancellationToken ct) =>
        {
            NoStore(ctx);
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

    /// <summary>
    /// The body of these responses contains the API key. Refuse intermediary
    /// caching so reverse proxies and CDNs don't keep a copy.
    /// </summary>
    private static void NoStore(HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl = "no-store, no-cache, private, must-revalidate";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";
    }
}

public sealed record ApiKeyDto(string ApiKey);
