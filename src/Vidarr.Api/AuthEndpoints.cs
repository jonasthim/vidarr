using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Api;

public static class AuthEndpoints
{
    public const string CookieName = "vidarr-session";
    public static readonly TimeSpan CookieTtl = TimeSpan.FromDays(7);

    public static IEndpointRouteBuilder MapVidarrAuthApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/auth");

        v1.MapGet("/status", async (HttpContext ctx, IApplicationConfigRepository repo, ISessionSigner signer, CancellationToken ct) =>
        {
            var cfg = await repo.GetAsync(ct);
            var enabled = string.Equals(cfg.AuthMethod, "Forms", StringComparison.OrdinalIgnoreCase);
            var authenticated = enabled
                && ctx.Request.Cookies.TryGetValue(CookieName, out var token)
                && !string.IsNullOrEmpty(cfg.SessionSecret)
                && signer.TryVerify(cfg.SessionSecret, token!, out _);
            return Results.Ok(new AuthStatusDto(
                Method: cfg.AuthMethod,
                Enabled: enabled,
                Authenticated: !enabled || authenticated,
                Username: enabled ? cfg.AuthUsername : null));
        });

        v1.MapPost("/login", async (
            LoginRequest req,
            HttpContext ctx,
            IApplicationConfigRepository repo,
            IPasswordHasher hasher,
            ISessionSigner signer,
            CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(req.Username) || string.IsNullOrEmpty(req.Password))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("credentials", "Username and password required")]));
            }
            var cfg = await repo.GetAsync(ct);
            if (!string.Equals(cfg.AuthMethod, "Forms", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(cfg.AuthUsername)
                || string.IsNullOrEmpty(cfg.AuthPasswordHash))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("auth", "Forms authentication is not enabled")]));
            }

            if (!string.Equals(cfg.AuthUsername, req.Username, StringComparison.Ordinal)
                || !hasher.Verify(req.Password, cfg.AuthPasswordHash))
            {
                return Results.Json(
                    new ApiErrorResponse([new ApiError("credentials", "Invalid credentials")]),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            if (string.IsNullOrEmpty(cfg.SessionSecret))
            {
                cfg.SessionSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                await repo.UpdateAsync(cfg, ct);
            }
            var token = signer.Sign(cfg.SessionSecret!, cfg.AuthUsername!, CookieTtl);
            ctx.Response.Cookies.Append(CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = CookieTtl,
                Path = "/",
            });
            return Results.Ok(new AuthStatusDto("Forms", true, true, cfg.AuthUsername));
        });

        v1.MapPost("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
            return Results.NoContent();
        });

        v1.MapPut("/config", async (
            AuthConfigRequest req,
            IApplicationConfigRepository repo,
            IPasswordHasher hasher,
            [FromServices] ApiKeyOptions apiKeyOptions,
            CancellationToken ct) =>
        {
            // PUT /auth/config requires the API key (enforced by UseApiKeyAuth) so the
            // operator can flip auth on/off without being locked out of the API surface.
            _ = apiKeyOptions; // silences unused; presence proves middleware ran

            var method = req.Method?.Trim() ?? "None";
            if (!string.Equals(method, "None", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "Forms", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("method", "Method must be None or Forms")]));
            }

            var cfg = await repo.GetAsync(ct);
            cfg.AuthMethod = method;
            if (string.Equals(method, "Forms", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(req.Username))
                {
                    return Results.BadRequest(new ApiErrorResponse([new ApiError("username", "Username required when enabling Forms auth")]));
                }
                cfg.AuthUsername = req.Username;
                if (!string.IsNullOrEmpty(req.Password))
                {
                    cfg.AuthPasswordHash = hasher.Hash(req.Password);
                }
                if (string.IsNullOrEmpty(cfg.AuthPasswordHash))
                {
                    return Results.BadRequest(new ApiErrorResponse([new ApiError("password", "Password required when enabling Forms auth for the first time")]));
                }
                if (string.IsNullOrEmpty(cfg.SessionSecret))
                {
                    cfg.SessionSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                }
            }
            else
            {
                cfg.AuthUsername = null;
                cfg.AuthPasswordHash = null;
            }
            await repo.UpdateAsync(cfg, ct);
            return Results.Ok(new AuthStatusDto(cfg.AuthMethod, string.Equals(cfg.AuthMethod, "Forms", StringComparison.OrdinalIgnoreCase),
                Authenticated: false, Username: cfg.AuthUsername));
        });

        return app;
    }
}

public sealed record AuthStatusDto(string Method, bool Enabled, bool Authenticated, string? Username);
public sealed record LoginRequest(string Username, string Password);
public sealed record AuthConfigRequest(string Method, string? Username, string? Password);
