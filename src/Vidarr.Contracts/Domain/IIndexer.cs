using Vidarr.Contracts.Models;

namespace Vidarr.Contracts.Domain;

public interface IIndexer
{
    int Id { get; }
    string Name { get; }
    DownloadProtocol Protocol { get; }
    bool SupportsRss { get; }
    bool SupportsSearch { get; }
    Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria criteria, CancellationToken ct);
    Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct);
    Task<IndexerTestResult> TestAsync(CancellationToken ct);
}

public sealed record IndexerTestResult(bool Success, string? Message);
