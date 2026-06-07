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

namespace Vidarr.Api.Tests;

public class ApiKeyEndpointsTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;

    public ApiKeyEndpointsTests()
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
                s.AddSingleton(new ApiKeyOverride(null));
                s.AddSingleton<IApiKeyService, ApiKeyService>();
            });
            web.Configure(app =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<VidarrDbContext>().Database.EnsureCreated();
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrApiKeyApi());
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
    public async Task Get_returns_persisted_key_generating_on_first_request()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/system/apikey"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<ApiKeyDto>();
        dto!.ApiKey.Should().HaveLength(32);

        var dto2 = await (await _client.GetAsync(new Uri("http://localhost/api/v1/system/apikey")))
            .Content.ReadFromJsonAsync<ApiKeyDto>();
        dto2!.ApiKey.Should().Be(dto.ApiKey);
    }

    [Fact]
    public async Task Rotate_returns_new_key_and_subsequent_get_reflects_it()
    {
        var first = await (await _client.GetAsync(new Uri("http://localhost/api/v1/system/apikey")))
            .Content.ReadFromJsonAsync<ApiKeyDto>();
        var rotateResp = await _client.PostAsync(new Uri("http://localhost/api/v1/system/apikey/rotate"), content: null);
        rotateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = await rotateResp.Content.ReadFromJsonAsync<ApiKeyDto>();
        rotated!.ApiKey.Should().NotBe(first!.ApiKey);

        var afterGet = await (await _client.GetAsync(new Uri("http://localhost/api/v1/system/apikey")))
            .Content.ReadFromJsonAsync<ApiKeyDto>();
        afterGet!.ApiKey.Should().Be(rotated.ApiKey);
    }
}

public class ApiKeyEndpointsOverrideTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;

    public ApiKeyEndpointsOverrideTests()
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
                s.AddSingleton(new ApiKeyOverride("env-fixed-key"));
                s.AddSingleton<IApiKeyService, ApiKeyService>();
            });
            web.Configure(app =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<VidarrDbContext>().Database.EnsureCreated();
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrApiKeyApi());
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
    public async Task Get_returns_override_value()
    {
        var dto = await (await _client.GetAsync(new Uri("http://localhost/api/v1/system/apikey")))
            .Content.ReadFromJsonAsync<ApiKeyDto>();
        dto!.ApiKey.Should().Be("env-fixed-key");
    }

    [Fact]
    public async Task Rotate_returns_bad_request_when_override_active()
    {
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/system/apikey/rotate"), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
