using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.Indexers;

public interface IReleaseSearchService
{
    Task<ReleaseSearchResult> SearchAsync(IndexerSearchCriteria criteria, CancellationToken ct);
    Task<ReleaseSearchResult> RssSyncAsync(CancellationToken ct);
}

public sealed record ReleaseSearchResult(
    IReadOnlyList<ReleaseInfo> Releases,
    IReadOnlyList<IndexerFailure> Failures,
    int IndexersQueried);

public sealed record IndexerFailure(int IndexerId, string IndexerName, string Reason);

public sealed class ReleaseSearchService : IReleaseSearchService
{
    private readonly IEnumerable<IIndexer> _indexers;
    private readonly TimeSpan _perIndexerTimeout;
    private readonly ILogger<ReleaseSearchService> _logger;

    public ReleaseSearchService(
        IEnumerable<IIndexer> indexers,
        ILogger<ReleaseSearchService> logger,
        TimeSpan? perIndexerTimeout = null)
    {
        _indexers = indexers;
        _perIndexerTimeout = perIndexerTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public Task<ReleaseSearchResult> SearchAsync(IndexerSearchCriteria criteria, CancellationToken ct) =>
        FanOutAsync(i => i.SupportsSearch ? i.FetchAsync(criteria, ct) : Task.FromResult<IReadOnlyList<ReleaseInfo>>([]), ct);

    public Task<ReleaseSearchResult> RssSyncAsync(CancellationToken ct) =>
        FanOutAsync(i => i.SupportsRss ? i.RssSyncAsync(ct) : Task.FromResult<IReadOnlyList<ReleaseInfo>>([]), ct);

    private async Task<ReleaseSearchResult> FanOutAsync(Func<IIndexer, Task<IReadOnlyList<ReleaseInfo>>> call, CancellationToken outerCt)
    {
        var indexers = _indexers.ToList();
        var failures = new List<IndexerFailure>();
        var releases = new List<ReleaseInfo>();

        var tasks = indexers.Select(async indexer =>
        {
            using var perCts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
            perCts.CancelAfter(_perIndexerTimeout);
            try
            {
                var items = await call(indexer).WaitAsync(perCts.Token);
                return (Indexer: indexer, Items: items, Error: (Exception?)null);
            }
            catch (OperationCanceledException) when (!outerCt.IsCancellationRequested)
            {
                return (Indexer: indexer, Items: (IReadOnlyList<ReleaseInfo>)[], Error: new TimeoutException("indexer timed out"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (Indexer: indexer, Items: (IReadOnlyList<ReleaseInfo>)[], Error: ex);
            }
        });

        var results = await Task.WhenAll(tasks);
        foreach (var (indexer, items, error) in results)
        {
            if (error is not null)
            {
                _logger.LogWarning(error, "Indexer {Name} failed: {Reason}", indexer.Name, error.Message);
                failures.Add(new IndexerFailure(indexer.Id, indexer.Name, error.Message));
                continue;
            }
            releases.AddRange(items);
        }

        var deduped = Dedupe(releases);
        return new ReleaseSearchResult(deduped, failures, indexers.Count);
    }

    /// <summary>
    /// Two releases collide when they share an indexer GUID, or absent that, the same
    /// (Title, IndexerName, SourceUrl) tuple. The first occurrence wins, which lines up
    /// with Sonarr's behaviour and the indexer-priority sort order callers apply.
    /// </summary>
    internal static IReadOnlyList<ReleaseInfo> Dedupe(IEnumerable<ReleaseInfo> releases)
    {
        var seenGuids = new HashSet<string>(StringComparer.Ordinal);
        var seenComposite = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<ReleaseInfo>();
        foreach (var r in releases)
        {
            var guid = r.ExtraMetadata.GetValueOrDefault("guid");
            if (!string.IsNullOrEmpty(guid))
            {
                if (!seenGuids.Add(guid))
                {
                    continue;
                }
            }
            var composite = $"{r.Title}|{r.IndexerName}|{r.SourceUrl.AbsoluteUri}";
            if (!seenComposite.Add(composite))
            {
                continue;
            }
            result.Add(r);
        }
        return result;
    }
}
