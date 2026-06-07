using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vidarr.Catalog;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Backup;

public sealed class BackupService : IBackupService
{
    private const string FileTimestampFormat = "yyyyMMdd-HHmmss";
    private const string FileExtension = ".zip";

    private readonly BackupOptions _options;
    private readonly ISystemClock _clock;
    private readonly IDbCheckpointer _checkpointer;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        BackupOptions options,
        ISystemClock clock,
        IDbCheckpointer checkpointer,
        ILogger<BackupService> logger)
    {
        _options = options;
        _clock = clock;
        _checkpointer = checkpointer;
        _logger = logger;
    }

    public async Task<BackupArtifact> CreateAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_options.BackupFolder);

        // Force a WAL checkpoint so the snapshot zip we take is point-in-time consistent.
        await _checkpointer.CheckpointAsync(ct);

        var now = _clock.UtcNow;
        var fileName = $"vidarr-{now.ToString(FileTimestampFormat, CultureInfo.InvariantCulture)}{FileExtension}";
        var target = Path.Combine(_options.BackupFolder, fileName);

        await using (var fs = File.Create(target))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
        {
            await AddFileIfPresent(zip, _options.SqliteSourcePath, "vidarr.db", ct);
            if (!string.IsNullOrEmpty(_options.ConfigSourcePath))
            {
                await AddFileIfPresent(zip, _options.ConfigSourcePath, "config.json", ct);
            }
        }

        var info = new FileInfo(target);
        var artifact = new BackupArtifact(target, info.Length, now);
        _logger.LogInformation("Backup created: {Path} ({Size} bytes)", target, info.Length);

        EnforceRetention();
        return artifact;
    }

    public Task<IReadOnlyList<BackupArtifact>> ListAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_options.BackupFolder))
        {
            return Task.FromResult<IReadOnlyList<BackupArtifact>>([]);
        }
        var files = Directory.EnumerateFiles(_options.BackupFolder, "*" + FileExtension)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(f => new BackupArtifact(f.FullName, f.Length, new DateTimeOffset(f.CreationTimeUtc, TimeSpan.Zero)))
            .ToArray();
        return Task.FromResult<IReadOnlyList<BackupArtifact>>(files);
    }

    public async Task<RestoreResult> StageRestoreAsync(Stream zipStream, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        if (!zipStream.CanSeek)
        {
            // Buffer to a seekable stream so ZipArchive can read the central directory.
            var ms = new MemoryStream();
            await zipStream.CopyToAsync(ms, ct);
            ms.Position = 0;
            zipStream = ms;
        }
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        var dbEntry = zip.GetEntry("vidarr.db")
            ?? throw new InvalidDataException("Backup archive does not contain vidarr.db");
        var configEntry = zip.GetEntry("config.json");

        var stagedDb = _options.SqliteSourcePath + ".restore";
        await ExtractEntryAsync(dbEntry, stagedDb, ct);

        string? stagedConfig = null;
        if (configEntry is not null && !string.IsNullOrEmpty(_options.ConfigSourcePath))
        {
            stagedConfig = _options.ConfigSourcePath + ".restore";
            await ExtractEntryAsync(configEntry, stagedConfig, ct);
        }

        _logger.LogInformation("Restore staged: {Db}{Config}; restart required",
            stagedDb, stagedConfig is null ? string.Empty : " + " + stagedConfig);
        return new RestoreResult(stagedDb, stagedConfig, RestartRequired: true);
    }

    public Task DeleteAsync(string fileName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new ArgumentException("File name must not contain path separators", nameof(fileName));
        }
        var path = Path.Combine(_options.BackupFolder, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("Backup deleted: {Path}", path);
        }
        return Task.CompletedTask;
    }

    private static async Task ExtractEntryAsync(ZipArchiveEntry entry, string target, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using var src = entry.Open();
        await using var dst = File.Create(target);
        await src.CopyToAsync(dst, ct);
    }

    private static async Task AddFileIfPresent(ZipArchive zip, string sourcePath, string entryName, CancellationToken ct)
    {
        if (!File.Exists(sourcePath)) return;
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var src = File.OpenRead(sourcePath);
        await using var dst = entry.Open();
        await src.CopyToAsync(dst, ct);
    }

    private void EnforceRetention()
    {
        if (_options.RetentionCount <= 0) return;
        var ordered = new DirectoryInfo(_options.BackupFolder)
            .EnumerateFiles("*" + FileExtension)
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToArray();
        foreach (var f in ordered.Skip(_options.RetentionCount))
        {
            try
            {
                f.Delete();
                _logger.LogInformation("Backup retention dropped {Path}", f.FullName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to delete old backup {Path}", f.FullName);
            }
        }
    }
}

public interface IDbCheckpointer
{
    Task CheckpointAsync(CancellationToken ct);
}

public sealed class SqliteWalCheckpointer : IDbCheckpointer
{
    private readonly VidarrDbContext _db;
    public SqliteWalCheckpointer(VidarrDbContext db) { _db = db; }
    public async Task CheckpointAsync(CancellationToken ct)
    {
        // SQLite-only: TRUNCATE compacts the -wal file before we zip the .db.
        try
        {
            await _db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);
        }
        catch (Exception)
        {
            // Best-effort; non-SQLite providers will throw and we still proceed.
        }
    }
}
