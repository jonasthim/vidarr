using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Events;
using Vidarr.Indexers;

namespace Vidarr.Health;

public sealed class IndexerReachableCheck : IHealthCheck
{
    private readonly IIndexerConfigRepository _repo;
    private readonly IEnumerable<IIndexerFactory> _factories;

    public IndexerReachableCheck(IIndexerConfigRepository repo, IEnumerable<IIndexerFactory> factories)
    {
        _repo = repo;
        _factories = factories;
    }

    public string Name => nameof(IndexerReachableCheck);

    public async Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct)
    {
        var byImpl = _factories.ToDictionary(f => f.Implementation, StringComparer.OrdinalIgnoreCase);
        var configs = await _repo.ListAsync(ct);
        var issues = new List<HealthIssue>();
        foreach (var cfg in configs)
        {
            if (!byImpl.TryGetValue(cfg.Implementation, out var factory))
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, cfg.Name),
                    HealthSeverity.Warning,
                    $"Indexer {cfg.Name} references unknown implementation {cfg.Implementation}"));
                continue;
            }

            try
            {
                var indexer = factory.Create(cfg.Id, cfg.Name, cfg.SettingsJson);
                var test = await indexer.TestAsync(ct);
                if (!test.Success)
                {
                    issues.Add(new HealthIssue(
                        new HealthIssueId(Name, cfg.Name),
                        HealthSeverity.Warning,
                        $"Indexer {cfg.Name}: {test.Message ?? "test failed"}"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, cfg.Name),
                    HealthSeverity.Warning,
                    $"Indexer {cfg.Name} threw on test: {ex.Message}"));
            }
        }
        return issues;
    }
}
