using Microsoft.AspNetCore.Http;
using Vidarr.Api;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Host;

/// <summary>
/// Serves the React shell's index.html with the live API key substituted into
/// the <c>%VIDARR_API_KEY%</c> placeholder at request time. The file is read
/// once per process and cached.
///
/// Security: the key is only embedded when either (a) Forms auth is disabled
/// (in which case the API key is the only secret), or (b) the request carries
/// a valid forms-auth session cookie. Otherwise an empty string is substituted
/// and the SPA's login page bootstraps the key after a successful login. This
/// prevents anonymous visitors to <c>/</c> from harvesting the key when forms
/// auth is supposed to be gating the application.
/// </summary>
public sealed class IndexHtmlHandler
{
    private const string Placeholder = "%VIDARR_API_KEY%";
    private readonly string _path;
    private string? _cachedTemplate;

    public IndexHtmlHandler(string indexHtmlPath)
    {
        _path = indexHtmlPath;
    }

    public bool Exists() => File.Exists(_path);

    public async Task<IResult> RenderAsync(
        HttpContext ctx,
        IApiKeyService keyService,
        IApplicationConfigRepository configRepo,
        ISessionSigner signer,
        CancellationToken ct)
    {
        var template = _cachedTemplate ??= await File.ReadAllTextAsync(_path, ct);
        var cfg = await configRepo.GetAsync(ct);
        var formsAuthOn = string.Equals(cfg.AuthMethod, "Forms", StringComparison.OrdinalIgnoreCase);

        string keyToEmbed = string.Empty;
        if (!formsAuthOn)
        {
            // No forms auth — the API key is the only credential. Embedding it
            // in the served HTML is the canonical bootstrap (parity with how
            // *arr-stack apps behave when forms auth is disabled).
            keyToEmbed = await keyService.GetCurrentAsync(ct);
        }
        else if (ctx.Request.Cookies.TryGetValue(AuthEndpoints.CookieName, out var token)
                 && !string.IsNullOrEmpty(cfg.SessionSecret)
                 && signer.TryVerify(cfg.SessionSecret!, token!, out _))
        {
            keyToEmbed = await keyService.GetCurrentAsync(ct);
        }

        var html = template.Replace(Placeholder, keyToEmbed, StringComparison.Ordinal);

        // The body carries a secret on the authenticated branch; refuse all
        // intermediary caching so reverse proxies / CDNs don't keep a copy.
        ctx.Response.Headers.CacheControl = "no-store, no-cache, private, must-revalidate";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers.Expires = "0";
        return Results.Content(html, "text/html; charset=utf-8");
    }
}
