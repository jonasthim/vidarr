using Microsoft.AspNetCore.Http;
using Vidarr.Api;

namespace Vidarr.Host;

/// <summary>
/// Serves the React shell's index.html with the live API key substituted into
/// the <c>%VIDARR_API_KEY%</c> placeholder at request time. The file is read
/// once per process and cached; the API key is fetched per request from
/// <see cref="IApiKeyService"/> so rotations take effect immediately.
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

    public async Task<IResult> RenderAsync(IApiKeyService svc, CancellationToken ct)
    {
        var template = _cachedTemplate ??= await File.ReadAllTextAsync(_path, ct);
        var apiKey = await svc.GetCurrentAsync(ct);
        var html = template.Replace(Placeholder, apiKey, StringComparison.Ordinal);
        return Results.Content(html, "text/html; charset=utf-8");
    }
}
