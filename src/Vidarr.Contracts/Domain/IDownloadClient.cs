using Vidarr.Contracts.Models;

namespace Vidarr.Contracts.Domain;

public interface IDownloadClient
{
    int Id { get; }
    string Name { get; }
    DownloadProtocol Protocol { get; }
    Task<DownloadClientItemId> DownloadAsync(RemoteRelease release, CancellationToken ct);
    Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct);
    Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct);
    Task<DownloadClientTestResult> TestAsync(CancellationToken ct);
}

public sealed record DownloadClientTestResult(bool Success, string? Message);
