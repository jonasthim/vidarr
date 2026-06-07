using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vidarr.Api;
using Vidarr.Catalog;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Host;
using Vidarr.Tests.Common;

namespace Vidarr.Api.Tests;

public class IndexHtmlEmbedTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _indexPath;
    private readonly SqliteConnection _conn;
    private readonly IHost _host;
    private readonly HttpClient _client;

    public IndexHtmlEmbedTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"vidarr-index-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _indexPath = Path.Combine(_tempRoot, "index.html");
        File.WriteAllText(_indexPath,
            "<!doctype html><html><head><script>window.VIDARR_API_KEY=\"%VIDARR_API_KEY%\";</script></head><body></body></html>");

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
                s.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
                s.AddSingleton<ISystemClock, FakeClock>();
                s.AddSingleton<ISessionSigner, HmacSessionSigner>();
                s.AddSingleton(new IndexHtmlHandler(_indexPath));
            });
            web.Configure(app =>
            {
                using var scope = app.ApplicationServices.CreateScope();
                scope.ServiceProvider.GetRequiredService<VidarrDbContext>().Database.EnsureCreated();
                app.UseRouting();
                app.UseEndpoints(e =>
                {
                    e.MapFallback(async (
                            HttpContext ctx,
                            IndexHtmlHandler handler,
                            IApiKeyService keyService,
                            IApplicationConfigRepository configRepo,
                            ISessionSigner signer) =>
                        await (await handler.RenderAsync(ctx, keyService, configRepo, signer, ctx.RequestAborted))
                            .ExecuteAsync(ctx));
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
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Embeds_key_when_forms_auth_disabled()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().NotContain("%VIDARR_API_KEY%");
        body.Should().MatchRegex("window\\.VIDARR_API_KEY=\"[a-f0-9]{32}\"");
    }

    [Fact]
    public async Task Sets_no_store_cache_headers()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/"));
        resp.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Suppresses_key_when_forms_auth_enabled_and_no_cookie()
    {
        // Flip forms auth on directly via the repo.
        using (var scope = _host.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IApplicationConfigRepository>();
            var cfg = await repo.GetAsync(default);
            cfg.AuthMethod = "Forms";
            cfg.SessionSecret = Convert.ToBase64String(new byte[32]);
            await repo.UpdateAsync(cfg, default);
        }

        var resp = await _client.GetAsync(new Uri("http://localhost/"));
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("window.VIDARR_API_KEY=\"\"");
    }

    [Fact]
    public async Task Embeds_key_when_forms_auth_enabled_and_cookie_valid()
    {
        string secret;
        using (var scope = _host.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IApplicationConfigRepository>();
            var cfg = await repo.GetAsync(default);
            cfg.AuthMethod = "Forms";
            secret = Convert.ToBase64String([.. Enumerable.Range(0, 32).Select(i => (byte)i)]);
            cfg.SessionSecret = secret;
            await repo.UpdateAsync(cfg, default);
        }

        var signer = _host.Services.GetRequiredService<ISessionSigner>();
        var token = signer.Sign(secret, "alice", TimeSpan.FromMinutes(5));

        var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
        req.Headers.Add("Cookie", $"{AuthEndpoints.CookieName}={token}");
        var resp = await _client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().MatchRegex("window\\.VIDARR_API_KEY=\"[a-f0-9]{32}\"");
    }
}
