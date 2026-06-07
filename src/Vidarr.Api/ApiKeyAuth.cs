using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Api;

/// <summary>
/// Legacy options record kept for the older two-argument <see cref="ApiKeyAuth.UseApiKeyAuth(IApplicationBuilder,ApiKeyOptions)"/>
/// overload used by some test fixtures. New code wires <see cref="IApiKeyService"/> instead.
/// </summary>
public sealed record ApiKeyOptions(string ApiKey);

public static class ApiKeyAuth
{
    public const string HeaderName = "X-Api-Key";
    public const string QueryName = "apikey";

    /// <summary>
    /// API-key middleware backed by IApiKeyService (DB-persisted + rotatable).
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/api"))
            {
                await next();
                return;
            }

            // Endpoints that do not require auth (parity with Sonarr):
            //   /api/v1/system/status — used by health probes
            //   /api/v1/auth/status   — shell decides whether to show login screen
            //   /api/v1/auth/login    — login itself can't require an existing session
            //   /api/v1/auth/logout   — logout clears cookies regardless of state
            if (path.StartsWithSegments("/api/v1/system/status")
                || path.StartsWithSegments("/api/v1/auth/status")
                || path.StartsWithSegments("/api/v1/auth/login")
                || path.StartsWithSegments("/api/v1/auth/logout"))
            {
                await next();
                return;
            }

            var keyService = context.RequestServices.GetService<IApiKeyService>();
            if (keyService is not null
                && TryGetSubmittedKey(context, out var submitted))
            {
                var expected = await keyService.GetCurrentAsync(context.RequestAborted);
                if (string.Equals(submitted, expected, StringComparison.Ordinal))
                {
                    await next();
                    return;
                }
            }

            // Cookie-session fallback when forms auth is enabled.
            if (await TryAuthenticateCookieAsync(context))
            {
                await next();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new ApiErrorResponse([new ApiError("apiKey", "Invalid or missing API key")]));
            await context.Response.WriteAsync(payload);
        });

    /// <summary>
    /// Compatibility shim — older test fixtures pass ApiKeyOptions explicitly.
    /// Wires up a temporary <see cref="IApiKeyService"/> backed by the override
    /// value so the rest of the pipeline behaves identically.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder app, ApiKeyOptions options) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/api"))
            {
                await next();
                return;
            }
            if (path.StartsWithSegments("/api/v1/system/status")
                || path.StartsWithSegments("/api/v1/auth/status")
                || path.StartsWithSegments("/api/v1/auth/login")
                || path.StartsWithSegments("/api/v1/auth/logout"))
            {
                await next();
                return;
            }
            if (TryGetSubmittedKey(context, out var submitted)
                && string.Equals(submitted, options.ApiKey, StringComparison.Ordinal))
            {
                await next();
                return;
            }
            if (await TryAuthenticateCookieAsync(context))
            {
                await next();
                return;
            }
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new ApiErrorResponse([new ApiError("apiKey", "Invalid or missing API key")]));
            await context.Response.WriteAsync(payload);
        });

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

    private static async Task<bool> TryAuthenticateCookieAsync(HttpContext ctx)
    {
        if (!ctx.Request.Cookies.TryGetValue(AuthEndpoints.CookieName, out var token) || string.IsNullOrEmpty(token))
        {
            return false;
        }
        var repo = ctx.RequestServices.GetService<IApplicationConfigRepository>();
        var signer = ctx.RequestServices.GetService<ISessionSigner>();
        if (repo is null || signer is null) return false;

        var cfg = await repo.GetAsync(ctx.RequestAborted);
        if (!string.Equals(cfg.AuthMethod, "Forms", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(cfg.SessionSecret))
        {
            return false;
        }
        return signer.TryVerify(cfg.SessionSecret!, token!, out _);
    }
}
