namespace Vidarr.Contracts.Abstractions;

public interface IHttpClient
{
    Task<HttpClientResponse> SendAsync(HttpClientRequest request, CancellationToken ct);
}

public sealed record HttpClientRequest(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, string>? Headers = null,
    HttpClientContent? Content = null,
    TimeSpan? Timeout = null);

public abstract record HttpClientContent
{
    public sealed record Json(string Body) : HttpClientContent;
    public sealed record Form(IReadOnlyDictionary<string, string> Fields) : HttpClientContent;
    public sealed record Bytes(string ContentType, byte[] Body) : HttpClientContent;
}

public sealed record HttpClientResponse(
    int StatusCode,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
