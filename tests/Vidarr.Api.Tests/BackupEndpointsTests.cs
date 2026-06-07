using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Api;
using Vidarr.Backup;
using Vidarr.Tests.Common;

namespace Vidarr.Api.Tests;

public class BackupEndpointsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IHost _host;
    private readonly HttpClient _client;

    public BackupEndpointsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"vidarr-backup-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        var db = Path.Combine(_tempRoot, "vidarr.db");
        File.WriteAllText(db, "fake");

        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddSingleton(new BackupOptions(
                    BackupFolder: Path.Combine(_tempRoot, "backups"),
                    SqliteSourcePath: db));
                s.AddSingleton<IDbCheckpointer, NoopCheckpointer>();
                s.AddSingleton<IBackupService>(sp => new BackupService(
                    sp.GetRequiredService<BackupOptions>(),
                    new FakeClock(),
                    sp.GetRequiredService<IDbCheckpointer>(),
                    NullLogger<BackupService>.Instance));
            });
            web.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrBackupApi());
            });
        }).Start();
        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task List_starts_empty_then_returns_created_artifact()
    {
        var empty = await _client.GetFromJsonAsync<BackupArtifactDto[]>(new Uri("http://localhost/api/v1/system/backup"));
        empty!.Should().BeEmpty();

        var created = await _client.PostAsync(new Uri("http://localhost/api/v1/system/backup"), content: null);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await _client.GetFromJsonAsync<BackupArtifactDto[]>(new Uri("http://localhost/api/v1/system/backup"));
        list!.Should().HaveCount(1);
    }

    [Fact]
    public async Task Post_restore_with_valid_zip_returns_accepted_and_stages_files()
    {
        var zip = BuildZip([("vidarr.db", "fresh"), ("config.json", "{}")]);
        var content = new ByteArrayContent(zip);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/system/backup/restore"), content);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var dto = await resp.Content.ReadFromJsonAsync<RestoreResultDto>();
        dto!.RestartRequired.Should().BeTrue();
        dto.StagedSqlite.Should().Be("vidarr.db.restore");
        dto.StagedConfig.Should().BeNull(); // no config configured in this test host
    }

    [Fact]
    public async Task Post_restore_without_db_returns_bad_request()
    {
        var zip = BuildZip([("config.json", "{}")]);
        var content = new ByteArrayContent(zip);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/system/backup/restore"), content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static byte[] BuildZip(IEnumerable<(string Name, string Content)> entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task Delete_existing_returns_no_content()
    {
        var created = await _client.PostAsync(new Uri("http://localhost/api/v1/system/backup"), content: null);
        created.EnsureSuccessStatusCode();
        var dto = await created.Content.ReadFromJsonAsync<BackupArtifactDto>();
        var resp = await _client.DeleteAsync(new Uri($"http://localhost/api/v1/system/backup/{dto!.FileName}"));
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed class NoopCheckpointer : IDbCheckpointer
    {
        public Task CheckpointAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
