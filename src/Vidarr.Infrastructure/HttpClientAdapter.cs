using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Infrastructure;

[ExcludeFromCodeCoverage(Justification = "Boundary adapter; covered by integration tests against the real HTTP stack.")]
public sealed class HttpClientAdapter : IHttpClient
{
    private readonly IHttpClientFactory _factory;

    public HttpClientAdapter(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<HttpClientResponse> SendAsync(HttpClientRequest request, CancellationToken ct)
    {
        var client = _factory.CreateClient("vidarr");
        if (request.Timeout is { } timeout)
        {
            client.Timeout = timeout;
        }

        using var message = new HttpRequestMessage(request.Method, request.Uri);

        if (request.Headers is not null)
        {
            foreach (var (name, value) in request.Headers)
            {
                message.Headers.TryAddWithoutValidation(name, value);
            }
        }

        if (request.Content is HttpClientContent.Json json)
        {
            message.Content = new StringContent(json.Body, Encoding.UTF8, "application/json");
        }
        else if (request.Content is HttpClientContent.Form form)
        {
            message.Content = new FormUrlEncodedContent(form.Fields);
        }
        else if (request.Content is HttpClientContent.Bytes bytes)
        {
            var byteContent = new ByteArrayContent(bytes.Body);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue(bytes.ContentType);
            message.Content = byteContent;
        }

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value));

        return new HttpClientResponse((int)response.StatusCode, headers, body);
    }
}
