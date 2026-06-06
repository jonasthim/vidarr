using Microsoft.Extensions.Logging;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Domain;

namespace Vidarr.DownloadClients;

public interface IDownloadClientRegistry
{
    /// <summary>
    /// Returns the runtime <see cref="IDownloadClient"/> instances backed by the persisted
    /// configuration rows whose <c>Enable</c> flag is true. Unknown implementations are
    /// logged and skipped so a config row written for a future client doesn't crash the
    /// poll loop.
    /// </summary>
    Task<IReadOnlyList<IDownloadClient>> GetActiveAsync(CancellationToken ct);
}

public sealed class DownloadClientRegistry : IDownloadClientRegistry
{
    private readonly IDownloadClientConfigRepository _repo;
    private readonly IEnumerable<IDownloadClientFactory> _factories;
    private readonly ILogger<DownloadClientRegistry> _logger;

    public DownloadClientRegistry(
        IDownloadClientConfigRepository repo,
        IEnumerable<IDownloadClientFactory> factories,
        ILogger<DownloadClientRegistry> logger)
    {
        _repo = repo;
        _factories = factories;
        _logger = logger;
    }

    public async Task<IReadOnlyList<IDownloadClient>> GetActiveAsync(CancellationToken ct)
    {
        var configs = await _repo.ListAsync(ct);
        var byImpl = _factories.ToDictionary(f => f.Implementation, StringComparer.OrdinalIgnoreCase);

        var live = new List<IDownloadClient>();
        foreach (var c in configs.Where(c => c.Enable))
        {
            if (!byImpl.TryGetValue(c.Implementation, out var factory))
            {
                _logger.LogWarning("Skipping download client {Name}: unknown implementation {Impl}", c.Name, c.Implementation);
                continue;
            }
            try
            {
                live.Add(factory.Create(c.Id, c.Name, c.SettingsJson));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to materialise download client {Name}", c.Name);
            }
        }
        return live;
    }
}
