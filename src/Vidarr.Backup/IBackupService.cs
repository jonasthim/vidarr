namespace Vidarr.Backup;

public sealed record BackupOptions(
    string BackupFolder,
    string SqliteSourcePath,
    string? ConfigSourcePath = null,
    int RetentionCount = 10);

public sealed record BackupArtifact(string Path, long SizeBytes, DateTimeOffset CreatedAt);

public sealed record RestoreResult(string SqliteStagedPath, string? ConfigStagedPath, bool RestartRequired);

public interface IBackupService
{
    Task<BackupArtifact> CreateAsync(CancellationToken ct);
    Task<IReadOnlyList<BackupArtifact>> ListAsync(CancellationToken ct);
    Task DeleteAsync(string fileName, CancellationToken ct);
    Task<RestoreResult> StageRestoreAsync(Stream zipStream, CancellationToken ct);
}
