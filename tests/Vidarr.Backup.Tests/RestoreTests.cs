using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Backup;
using Vidarr.Tests.Common;

namespace Vidarr.Backup.Tests;

public class RestoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _dbPath;
    private readonly string _configPath;

    public RestoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"vidarr-restore-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _dbPath = Path.Combine(_tempRoot, "vidarr.db");
        _configPath = Path.Combine(_tempRoot, "config.json");
        File.WriteAllText(_dbPath, "original-db");
        File.WriteAllText(_configPath, "{\"original\":true}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
        GC.SuppressFinalize(this);
    }

    private BackupService NewService() => new(
        new BackupOptions(Path.Combine(_tempRoot, "backups"), _dbPath, _configPath),
        new FakeClock(),
        new NoopCheckpointer(),
        NullLogger<BackupService>.Instance);

    private static byte[] BuildZip(IEnumerable<(string Name, string Content)> entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
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
    public async Task Stage_writes_db_and_config_alongside_originals()
    {
        var zip = BuildZip([("vidarr.db", "fresh-db"), ("config.json", "{\"fresh\":true}")]);
        var svc = NewService();
        using var ms = new MemoryStream(zip);
        var result = await svc.StageRestoreAsync(ms, default);

        File.ReadAllText(result.SqliteStagedPath).Should().Be("fresh-db");
        File.ReadAllText(result.ConfigStagedPath!).Should().Be("{\"fresh\":true}");
        result.RestartRequired.Should().BeTrue();
        // Live files untouched until restart.
        File.ReadAllText(_dbPath).Should().Be("original-db");
    }

    [Fact]
    public async Task Stage_works_without_config_entry()
    {
        var zip = BuildZip([("vidarr.db", "fresh-db")]);
        var svc = NewService();
        using var ms = new MemoryStream(zip);
        var result = await svc.StageRestoreAsync(ms, default);
        result.ConfigStagedPath.Should().BeNull();
    }

    [Fact]
    public async Task Stage_throws_when_db_entry_missing()
    {
        var zip = BuildZip([("config.json", "{}")]);
        var svc = NewService();
        using var ms = new MemoryStream(zip);
        await Assert.ThrowsAsync<InvalidDataException>(() => svc.StageRestoreAsync(ms, default));
    }

    [Fact]
    public async Task Stage_supports_non_seekable_stream()
    {
        var zip = BuildZip([("vidarr.db", "non-seekable")]);
        var svc = NewService();
        using var src = new MemoryStream(zip);
        using var nonSeekable = new NonSeekableStream(src);
        var result = await svc.StageRestoreAsync(nonSeekable, default);
        File.ReadAllText(result.SqliteStagedPath).Should().Be("non-seekable");
    }

    [Fact]
    public void Bootstrap_promotes_staged_files_and_keeps_pre_restore_copy()
    {
        File.WriteAllText(_dbPath + ".restore", "restored-db");
        File.WriteAllText(_configPath + ".restore", "{\"restored\":true}");
        RestoreBootstrap.ApplyPendingRestore(_dbPath, _configPath).Should().BeTrue();

        File.ReadAllText(_dbPath).Should().Be("restored-db");
        File.ReadAllText(_configPath).Should().Be("{\"restored\":true}");
        File.ReadAllText(_dbPath + ".pre-restore").Should().Be("original-db");
        File.Exists(_dbPath + ".restore").Should().BeFalse();
    }

    [Fact]
    public void Bootstrap_overwrites_pre_existing_pre_restore()
    {
        File.WriteAllText(_dbPath + ".pre-restore", "stale-rescue");
        File.WriteAllText(_dbPath + ".restore", "new-db");
        RestoreBootstrap.ApplyPendingRestore(_dbPath, null).Should().BeTrue();
        File.ReadAllText(_dbPath + ".pre-restore").Should().Be("original-db");
    }

    [Fact]
    public void Bootstrap_is_no_op_when_no_staged_files()
    {
        RestoreBootstrap.ApplyPendingRestore(_dbPath, _configPath).Should().BeFalse();
    }

    [Fact]
    public void Bootstrap_handles_missing_live_file()
    {
        File.Delete(_dbPath);
        File.WriteAllText(_dbPath + ".restore", "fresh");
        RestoreBootstrap.ApplyPendingRestore(_dbPath, null).Should().BeTrue();
        File.ReadAllText(_dbPath).Should().Be("fresh");
        File.Exists(_dbPath + ".pre-restore").Should().BeFalse();
    }

    private sealed class NoopCheckpointer : IDbCheckpointer
    {
        public Task CheckpointAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
