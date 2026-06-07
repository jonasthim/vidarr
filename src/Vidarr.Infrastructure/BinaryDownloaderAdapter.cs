using System.Diagnostics.CodeAnalysis;
using Vidarr.Health;

namespace Vidarr.Infrastructure;

[ExcludeFromCodeCoverage(Justification = "Boundary adapter; covered by integration tests against the real HTTP stack.")]
public sealed class BinaryDownloaderAdapter : IBinaryDownloader
{
    private readonly IHttpClientFactory _factory;

    public BinaryDownloaderAdapter(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<byte[]> DownloadAsync(Uri url, IReadOnlyDictionary<string, string>? headers, CancellationToken ct)
    {
        var client = _factory.CreateClient("vidarr");
        using var msg = new HttpRequestMessage(HttpMethod.Get, url);
        if (headers is not null)
        {
            foreach (var (k, v) in headers) msg.Headers.TryAddWithoutValidation(k, v);
        }
        using var resp = await client.SendAsync(msg, HttpCompletionOption.ResponseContentRead, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
}
