using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Health.Tests;

internal sealed class FakeRootFolderRepository : IRootFolderRepository
{
    public List<RootFolder> Items { get; } = [];
    public Task<IReadOnlyList<RootFolder>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RootFolder>>(Items);
    public Task<RootFolder?> GetAsync(int id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));
    public Task<RootFolder> AddAsync(RootFolder folder, CancellationToken ct)
    {
        folder.Id = Items.Count + 1;
        Items.Add(folder);
        return Task.FromResult(folder);
    }
    public Task DeleteAsync(int id, CancellationToken ct)
    {
        Items.RemoveAll(r => r.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeIndexerConfigRepository : IIndexerConfigRepository
{
    public List<IndexerConfig> Items { get; } = [];
    public Task<IReadOnlyList<IndexerConfig>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IndexerConfig>>(Items);
    public Task<IndexerConfig?> GetAsync(int id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task<IndexerConfig> AddAsync(IndexerConfig cfg, CancellationToken ct)
    {
        cfg.Id = Items.Count + 1;
        Items.Add(cfg);
        return Task.FromResult(cfg);
    }
    public Task UpdateAsync(IndexerConfig cfg, CancellationToken ct) => Task.CompletedTask;
    public Task DeleteAsync(int id, CancellationToken ct)
    {
        Items.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeDownloadClientConfigRepository : IDownloadClientConfigRepository
{
    public List<DownloadClientConfig> Items { get; } = [];
    public Task<IReadOnlyList<DownloadClientConfig>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DownloadClientConfig>>(Items);
    public Task<DownloadClientConfig?> GetAsync(int id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task<DownloadClientConfig> AddAsync(DownloadClientConfig cfg, CancellationToken ct)
    {
        cfg.Id = Items.Count + 1;
        Items.Add(cfg);
        return Task.FromResult(cfg);
    }
    public Task UpdateAsync(DownloadClientConfig cfg, CancellationToken ct) => Task.CompletedTask;
    public Task DeleteAsync(int id, CancellationToken ct)
    {
        Items.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }
}
