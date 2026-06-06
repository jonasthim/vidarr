using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Events;
using Vidarr.DownloadClients;

namespace Vidarr.Health;

public sealed class DownloadClientReachableCheck : IHealthCheck
{
    private readonly IDownloadClientConfigRepository _repo;
    private readonly IEnumerable<IDownloadClientFactory> _factories;

    public DownloadClientReachableCheck(IDownloadClientConfigRepository repo, IEnumerable<IDownloadClientFactory> factories)
    {
        _repo = repo;
        _factories = factories;
    }

    public string Name => nameof(DownloadClientReachableCheck);

    public async Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct)
    {
        var byImpl = _factories.ToDictionary(f => f.Implementation, StringComparer.OrdinalIgnoreCase);
        var configs = await _repo.ListAsync(ct);
        var issues = new List<HealthIssue>();
        foreach (var cfg in configs.Where(c => c.Enable))
        {
            if (!byImpl.TryGetValue(cfg.Implementation, out var factory))
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, cfg.Name),
                    HealthSeverity.Warning,
                    $"Download client {cfg.Name} references unknown implementation {cfg.Implementation}"));
                continue;
            }

            try
            {
                var client = factory.Create(cfg.Id, cfg.Name, cfg.SettingsJson);
                var test = await client.TestAsync(ct);
                if (!test.Success)
                {
                    issues.Add(new HealthIssue(
                        new HealthIssueId(Name, cfg.Name),
                        HealthSeverity.Warning,
                        $"Download client {cfg.Name}: {test.Message ?? "test failed"}"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                issues.Add(new HealthIssue(
                    new HealthIssueId(Name, cfg.Name),
                    HealthSeverity.Warning,
                    $"Download client {cfg.Name} threw on test: {ex.Message}"));
            }
        }
        return issues;
    }
}
