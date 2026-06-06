using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vidarr.Api;

namespace Vidarr.Api.Tests;

public class ApiKeyAuthTests
{
    private static HttpClient BuildClient(string apiKey, RequestDelegate routeImpl)
    {
        var builder = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.Configure(app =>
            {
                app.UseApiKeyAuth(new ApiKeyOptions(apiKey));
                app.Use(async (ctx, next) =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/api"))
                    {
                        await routeImpl(ctx);
                        return;
                    }
                    await next();
                });
            });
        });
        var host = builder.Start();
        return host.GetTestClient();
    }

    [Fact]
    public async Task Request_without_api_key_returns_401()
    {
        using var client = BuildClient("secret", ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });
        var resp = await client.GetAsync(new Uri("http://localhost/api/v1/artist"));
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid or missing API key");
    }

    [Fact]
    public async Task Request_with_correct_header_passes()
    {
        using var client = BuildClient("secret", ctx =>
        {
            ctx.Response.StatusCode = 200;
            return ctx.Response.WriteAsync("ok");
        });
        client.DefaultRequestHeaders.Add(ApiKeyAuth.HeaderName, "secret");
        var resp = await client.GetAsync(new Uri("http://localhost/api/v1/artist"));
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Be("ok");
    }

    [Fact]
    public async Task Request_with_query_string_apikey_passes()
    {
        using var client = BuildClient("secret", ctx =>
        {
            ctx.Response.StatusCode = 200;
            return ctx.Response.WriteAsync("ok");
        });
        var resp = await client.GetAsync(new Uri("http://localhost/api/v1/artist?apikey=secret"));
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task System_status_endpoint_is_accessible_without_key()
    {
        using var client = BuildClient("secret", ctx =>
        {
            ctx.Response.StatusCode = 200;
            return ctx.Response.WriteAsync("ok");
        });
        var resp = await client.GetAsync(new Uri("http://localhost/api/v1/system/status"));
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task Non_api_paths_are_not_protected()
    {
        using var client = BuildClient("secret", ctx =>
        {
            ctx.Response.StatusCode = 418;
            return ctx.Response.WriteAsync("teapot");
        });
        var resp = await client.GetAsync(new Uri("http://localhost/anything"));
        // Falls through past the api-key middleware to the next pipeline step.
        resp.StatusCode.Should().NotBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_api_key_returns_401()
    {
        using var client = BuildClient("secret", ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });
        client.DefaultRequestHeaders.Add(ApiKeyAuth.HeaderName, "wrong");
        var resp = await client.GetAsync(new Uri("http://localhost/api/v1/artist"));
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}
