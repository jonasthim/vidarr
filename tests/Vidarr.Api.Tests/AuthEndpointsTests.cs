using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

public class AuthEndpointsTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;
    private const string ApiKey = "test-key";

    public AuthEndpointsTests()
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
                app.UseEndpoints(e => e.MapVidarrAuthApi());
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

    private async Task ConfigureFormsAuthAsync(string username, string password)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/v1/auth/config")
        {
            Content = JsonContent.Create(new AuthConfigRequest("Forms", username, password)),
        };
        req.Headers.Add(ApiKeyAuth.HeaderName, ApiKey);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Status_reports_disabled_when_none()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/auth/status"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<AuthStatusDto>();
        dto.Should().NotBeNull();
        dto!.Method.Should().Be("None");
        dto.Enabled.Should().BeFalse();
        dto.Authenticated.Should().BeTrue();
    }

    [Fact]
    public async Task Login_succeeds_with_valid_credentials_and_status_then_authenticated()
    {
        await ConfigureFormsAuthAsync("alice", "hunter2");

        var login = await _client.PostAsJsonAsync("http://localhost/api/v1/auth/login",
            new LoginRequest("alice", "hunter2"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Headers.TryGetValues("Set-Cookie", out var setCookie).Should().BeTrue();
        var cookie = setCookie!.First().Split(';')[0];

        var statusReq = new HttpRequestMessage(HttpMethod.Get, "http://localhost/api/v1/auth/status");
        statusReq.Headers.Add("Cookie", cookie);
        var statusResp = await _client.SendAsync(statusReq);
        var status = await statusResp.Content.ReadFromJsonAsync<AuthStatusDto>();
        status!.Authenticated.Should().BeTrue();
        status.Enabled.Should().BeTrue();
        status.Username.Should().Be("alice");
    }

    [Fact]
    public async Task Login_rejects_wrong_password()
    {
        await ConfigureFormsAuthAsync("alice", "hunter2");
        var login = await _client.PostAsJsonAsync("http://localhost/api/v1/auth/login",
            new LoginRequest("alice", "wrong"));
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_rejects_unknown_user()
    {
        await ConfigureFormsAuthAsync("alice", "hunter2");
        var login = await _client.PostAsJsonAsync("http://localhost/api/v1/auth/login",
            new LoginRequest("mallory", "hunter2"));
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_fails_when_forms_auth_not_enabled()
    {
        var login = await _client.PostAsJsonAsync("http://localhost/api/v1/auth/login",
            new LoginRequest("alice", "hunter2"));
        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_requires_username_and_password()
    {
        await ConfigureFormsAuthAsync("alice", "hunter2");
        var login = await _client.PostAsJsonAsync("http://localhost/api/v1/auth/login",
            new LoginRequest("", ""));
        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_clears_cookie()
    {
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/auth/logout"), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        resp.Headers.TryGetValues("Set-Cookie", out var setCookie).Should().BeTrue();
        setCookie!.First().Should().Contain("vidarr-session=;").And.Contain("expires=");
    }

    [Fact]
    public async Task Put_config_rejects_unknown_method()
    {
        var req = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/v1/auth/config")
        {
            Content = JsonContent.Create(new AuthConfigRequest("Bogus", "u", "p")),
        };
        req.Headers.Add(ApiKeyAuth.HeaderName, ApiKey);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_config_forms_requires_username()
    {
        var req = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/v1/auth/config")
        {
            Content = JsonContent.Create(new AuthConfigRequest("Forms", null, "p")),
        };
        req.Headers.Add(ApiKeyAuth.HeaderName, ApiKey);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_config_forms_requires_password_on_first_setup()
    {
        var req = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/v1/auth/config")
        {
            Content = JsonContent.Create(new AuthConfigRequest("Forms", "alice", null)),
        };
        req.Headers.Add(ApiKeyAuth.HeaderName, ApiKey);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_config_to_none_clears_credentials()
    {
        await ConfigureFormsAuthAsync("alice", "hunter2");
        var req = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/v1/auth/config")
        {
            Content = JsonContent.Create(new AuthConfigRequest("None", null, null)),
        };
        req.Headers.Add(ApiKeyAuth.HeaderName, ApiKey);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await resp.Content.ReadFromJsonAsync<AuthStatusDto>();
        status!.Method.Should().Be("None");
        status.Username.Should().BeNull();
    }

    [Fact]
    public async Task Config_endpoint_requires_api_key()
    {
        // No header → should be rejected by UseApiKeyAuth.
        var req = new HttpRequestMessage(HttpMethod.Put, "http://localhost/api/v1/auth/config")
        {
            Content = JsonContent.Create(new AuthConfigRequest("None", null, null)),
        };
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
