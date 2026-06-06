using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;

namespace Vidarr.Health;

public sealed class DiskSpaceCheck : IHealthCheck
{
    public const double WarnFraction = 0.10;
    public const double ErrorFraction = 0.05;

    private readonly IRootFolderRepository _rootFolders;
    private readonly IFileSystem _fileSystem;

    public DiskSpaceCheck(IRootFolderRepository rootFolders, IFileSystem fileSystem)
    {
        _rootFolders = rootFolders;
        _fileSystem = fileSystem;
    }

    public string Name => nameof(DiskSpaceCheck);

    public async Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct)
    {
        var folders = await _rootFolders.ListAsync(ct);
        var issues = new List<HealthIssue>();
        foreach (var folder in folders)
        {
            if (!_fileSystem.DirectoryExists(folder.Path))
            {
                continue; // RootFolderAccessibleCheck owns this surface
            }
            DiskInfo info;
            try
            {
                info = _fileSystem.GetDiskInfo(folder.Path);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, folder.Path),
                    HealthSeverity.Warning,
                    $"Cannot read disk info for {folder.Path}: {ex.Message}"));
                continue;
            }

            if (info.TotalBytes <= 0) continue;
            var freeFraction = (double)info.FreeBytes / info.TotalBytes;
            if (freeFraction < ErrorFraction)
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, folder.Path),
                    HealthSeverity.Error,
                    $"Root folder {folder.Path} has {freeFraction:P0} free ({info.FreeBytes:N0}/{info.TotalBytes:N0} bytes)"));
            }
            else if (freeFraction < WarnFraction)
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, folder.Path),
                    HealthSeverity.Warning,
                    $"Root folder {folder.Path} has {freeFraction:P0} free"));
            }
        }
        return issues;
    }
}
