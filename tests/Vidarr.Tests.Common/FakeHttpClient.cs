using System.Collections.Concurrent;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Tests.Common;

public sealed class FakeHttpClient : IHttpClient
{
    private readonly List<HttpClientRequest> _requests = [];
    private readonly List<HttpStubRule> _rules = [];
    private HttpClientResponse _default = new(404, new Dictionary<string, string>(), string.Empty);

    public IReadOnlyList<HttpClientRequest> Requests => _requests;

    public FakeHttpClient WhenRequest(Func<HttpClientRequest, bool> predicate, HttpClientResponse response)
    {
        _rules.Add(new HttpStubRule(predicate, _ => Task.FromResult(response)));
        return this;
    }

    public FakeHttpClient WhenRequest(Func<HttpClientRequest, bool> predicate, Func<HttpClientRequest, Task<HttpClientResponse>> respond)
    {
        _rules.Add(new HttpStubRule(predicate, respond));
        return this;
    }

    public FakeHttpClient SetDefault(HttpClientResponse response)
    {
        _default = response;
        return this;
    }

    public async Task<HttpClientResponse> SendAsync(HttpClientRequest request, CancellationToken ct)
    {
        _requests.Add(request);
        foreach (var rule in _rules)
        {
            if (rule.Predicate(request))
            {
                return await rule.Respond(request);
            }
        }
        return _default;
    }

    private sealed record HttpStubRule(Func<HttpClientRequest, bool> Predicate, Func<HttpClientRequest, Task<HttpClientResponse>> Respond);
}

public static class HttpClientResponseFactory
{
    public static HttpClientResponse Json(string body, int status = 200) =>
        new(status, new Dictionary<string, string> { ["Content-Type"] = "application/json" }, body);

    public static HttpClientResponse Text(string body, int status = 200) =>
        new(status, new Dictionary<string, string> { ["Content-Type"] = "text/plain" }, body);

    public static HttpClientResponse Xml(string body, int status = 200) =>
        new(status, new Dictionary<string, string> { ["Content-Type"] = "application/xml" }, body);
}
