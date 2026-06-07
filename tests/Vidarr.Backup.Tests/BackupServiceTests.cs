using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Backup;
using Vidarr.Tests.Common;

namespace Vidarr.Backup.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _backupFolder;
    private readonly string _dbPath;
    private readonly string _configPath;
    private readonly FakeClock _clock = new();

    public BackupServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"vidarr-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _backupFolder = Path.Combine(_tempRoot, "backups");
        _dbPath = Path.Combine(_tempRoot, "vidarr.db");
        _configPath = Path.Combine(_tempRoot, "config.json");
        File.WriteAllBytes(_dbPath, [.. Enumerable.Range(0, 256).Select(i => (byte)i)]);
        File.WriteAllText(_configPath, "{\"hello\":\"world\"}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private BackupService NewService(int retention = 10) => new(
        new BackupOptions(_backupFolder, _dbPath, _configPath, retention),
        _clock,
        new NoopCheckpointer(),
        NullLogger<BackupService>.Instance);

    [Fact]
    public async Task Create_writes_zip_containing_db_and_config()
    {
        var svc = NewService();
        var artifact = await svc.CreateAsync(default);

        File.Exists(artifact.Path).Should().BeTrue();
        using var fs = File.OpenRead(artifact.Path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.Name).Should().BeEquivalentTo(["vidarr.db", "config.json"]);
        var configEntry = zip.GetEntry("config.json")!;
        using var reader = new StreamReader(configEntry.Open());
        (await reader.ReadToEndAsync()).Should().Be("{\"hello\":\"world\"}");
    }

    [Fact]
    public async Task Create_works_without_config_file()
    {
        File.Delete(_configPath);
        var svc = NewService();
        var artifact = await svc.CreateAsync(default);
        using var zip = ZipFile.OpenRead(artifact.Path);
        zip.Entries.Select(e => e.Name).Should().BeEquivalentTo(["vidarr.db"]);
    }

    [Fact]
    public async Task Create_invokes_checkpointer()
    {
        var checkpointer = new SpyCheckpointer();
        var svc = new BackupService(
            new BackupOptions(_backupFolder, _dbPath, _configPath),
            _clock,
            checkpointer,
            NullLogger<BackupService>.Instance);
        await svc.CreateAsync(default);
        checkpointer.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Create_enforces_retention_count()
    {
        var svc = NewService(retention: 2);
        await svc.CreateAsync(default);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await svc.CreateAsync(default);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await svc.CreateAsync(default);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await svc.CreateAsync(default);

        var zips = Directory.EnumerateFiles(_backupFolder, "*.zip").ToArray();
        zips.Should().HaveCount(2);
    }

    [Fact]
    public async Task Retention_zero_keeps_all()
    {
        var svc = NewService(retention: 0);
        await svc.CreateAsync(default);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await svc.CreateAsync(default);
        Directory.EnumerateFiles(_backupFolder, "*.zip").Should().HaveCount(2);
    }

    [Fact]
    public async Task List_returns_artifacts_newest_first()
    {
        var svc = NewService();
        await svc.CreateAsync(default);
        var firstFile = Directory.EnumerateFiles(_backupFolder, "*.zip").First();
        File.SetCreationTimeUtc(firstFile, DateTime.UtcNow.AddMinutes(-10));
        _clock.Advance(TimeSpan.FromSeconds(1));
        await svc.CreateAsync(default);
        var second = Directory.EnumerateFiles(_backupFolder, "*.zip").OrderByDescending(File.GetCreationTimeUtc).First();
        File.SetCreationTimeUtc(second, DateTime.UtcNow);

        var list = await svc.ListAsync(default);
        list.Should().HaveCount(2);
        list[0].Path.Should().Be(second);
    }

    [Fact]
    public async Task List_returns_empty_when_folder_missing()
    {
        var svc = new BackupService(
            new BackupOptions(Path.Combine(_tempRoot, "nope"), _dbPath, _configPath),
            _clock,
            new NoopCheckpointer(),
            NullLogger<BackupService>.Instance);
        (await svc.ListAsync(default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_removes_named_file()
    {
        var svc = NewService();
        var artifact = await svc.CreateAsync(default);
        var name = Path.GetFileName(artifact.Path);

        await svc.DeleteAsync(name, default);
        File.Exists(artifact.Path).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_with_path_separator_throws()
    {
        var svc = NewService();
        await Assert.ThrowsAsync<ArgumentException>(() => svc.DeleteAsync("../escape.zip", default));
    }

    [Fact]
    public async Task Delete_silently_succeeds_when_file_missing()
    {
        var svc = NewService();
        await svc.CreateAsync(default);
        await svc.DeleteAsync("does-not-exist.zip", default);
    }

    private sealed class NoopCheckpointer : IDbCheckpointer
    {
        public Task CheckpointAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class SpyCheckpointer : IDbCheckpointer
    {
        public int Calls;
        public Task CheckpointAsync(CancellationToken ct) { Calls++; return Task.CompletedTask; }
    }
}
