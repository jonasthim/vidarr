using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Vidarr.Api;

public sealed record ApiKeyOptions(string ApiKey);

public static class ApiKeyAuth
{
    public const string HeaderName = "X-Api-Key";
    public const string QueryName = "apikey";

    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app, ApiKeyOptions options)
    {
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await next();
                return;
            }

            // /api/v1/system/status is reachable without an api key (parity with Sonarr).
            if (context.Request.Path.StartsWithSegments("/api/v1/system/status"))
            {
                await next();
                return;
            }

            if (TryGetSubmittedKey(context, out var submitted) && string.Equals(submitted, options.ApiKey, StringComparison.Ordinal))
            {
                await next();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new ApiErrorResponse([new ApiError("apiKey", "Invalid or missing API key")]));
            await context.Response.WriteAsync(payload);
        });
    }

    private static bool TryGetSubmittedKey(HttpContext ctx, out string? key)
    {
        if (ctx.Request.Headers.TryGetValue(HeaderName, out var headerValues) && !string.IsNullOrEmpty(headerValues.ToString()))
        {
            key = headerValues.ToString();
            return true;
        }
        if (ctx.Request.Query.TryGetValue(QueryName, out var qv) && !string.IsNullOrEmpty(qv.ToString()))
        {
            key = qv.ToString();
            return true;
        }
        key = null;
        return false;
    }
}
