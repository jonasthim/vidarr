using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vidarr.Api;
using Vidarr.Catalog;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Tests.Common;

namespace Vidarr.Api.Tests;

public class ApiKeyAuthCookieFallbackTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;
    private const string ApiKey = "test-key";

    public ApiKeyAuthCookieFallbackTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddDbContext<VidarrDbContext>(o => o.UseSqlite(_conn));
                s.AddScoped<IApplicationConfigRepository, ApplicationConfigRepository>();
                s.AddSingleton(new ApiKeyOptions(ApiKey));
                s.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
                s.AddSingleton<ISystemClock, FakeClock>();
                s.AddSingleton<ISessionSigner, HmacSessionSigner>();
            });
            web.Configure(app =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<VidarrDbContext>().Database.EnsureCreated();
                app.UseApiKeyAuth(new ApiKeyOptions(ApiKey));
                app.UseRouting();
                app.UseEndpoints(e =>
                {
                    e.MapVidarrAuthApi();
                    e.MapGet("/api/v1/ping", () => Results.Ok("pong"));
                });
            });
        }).Start();
        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Protected_endpoint_rejects_anonymous_requests()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/ping"));
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_accepts_session_cookie_after_login()
    {
        // Enable forms-auth with api key.
        using (var setup = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/v1/auth/config")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new AuthConfigRequest("Forms", "alice", "hunter2")),
        })
        {
            setup.Headers.Add(ApiKeyAuth.HeaderName, ApiKey);
            (await _client.SendAsync(setup)).EnsureSuccessStatusCode();
        }

        // Log in (no api key required) — extract Set-Cookie.
        var login = await _client.PostAsJsonAsync("http://localhost/api/v1/auth/login",
            new LoginRequest("alice", "hunter2"));
        login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        // Cookie alone should now satisfy the API-key middleware.
        var pingReq = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/ping");
        pingReq.Headers.Add("Cookie", cookie);
        var ping = await _client.SendAsync(pingReq);
        ping.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
