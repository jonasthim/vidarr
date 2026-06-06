using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;

namespace Vidarr.Health;

public sealed class RootFolderAccessibleCheck : IHealthCheck
{
    private readonly IRootFolderRepository _repo;
    private readonly IFileSystem _fileSystem;

    public RootFolderAccessibleCheck(IRootFolderRepository repo, IFileSystem fileSystem)
    {
        _repo = repo;
        _fileSystem = fileSystem;
    }

    public string Name => nameof(RootFolderAccessibleCheck);

    public async Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct)
    {
        var folders = await _repo.ListAsync(ct);
        var issues = new List<HealthIssue>();
        foreach (var folder in folders)
        {
            if (!_fileSystem.DirectoryExists(folder.Path))
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, folder.Path),
                    HealthSeverity.Error,
                    $"Root folder {folder.Path} does not exist"));
            }
        }
        return issues;
    }
}
