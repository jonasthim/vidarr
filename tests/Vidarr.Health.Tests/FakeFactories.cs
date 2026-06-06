using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Indexers;

namespace Vidarr.Health.Tests;

internal sealed class FakeIndexerFactory : IIndexerFactory
{
    private readonly Func<IIndexer> _create;
    public FakeIndexerFactory(string implementation, Func<IIndexer> create)
    {
        Implementation = implementation;
        _create = create;
    }
    public string Implementation { get; }
    public string DisplayName => Implementation;
    public IReadOnlyList<IndexerFieldSchema> SettingsSchema { get; } = [];
    public IIndexer Create(int id, string name, string settingsJson) => _create();
}

internal sealed class FakeIndexer : IIndexer
{
    private readonly IndexerTestResult? _result;
    private readonly Exception? _throws;
    public FakeIndexer(IndexerTestResult result) { _result = result; }
    public FakeIndexer(Exception throws) { _throws = throws; }
    public int Id => 0;
    public string Name => "fake";
    public DownloadProtocol Protocol => DownloadProtocol.Torrent;
    public bool SupportsRss => false;
    public bool SupportsSearch => true;
    public Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria criteria, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
    public Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReleaseInfo>>([]);
    public Task<IndexerTestResult> TestAsync(CancellationToken ct) =>
        _throws is not null ? Task.FromException<IndexerTestResult>(_throws) : Task.FromResult(_result!);
}

internal sealed class FakeDownloadClientFactory : IDownloadClientFactory
{
    private readonly Func<IDownloadClient> _create;
    public FakeDownloadClientFactory(string implementation, Func<IDownloadClient> create)
    {
        Implementation = implementation;
        _create = create;
    }
    public string Implementation { get; }
    public string DisplayName => Implementation;
    public DownloadProtocol Protocol => DownloadProtocol.Torrent;
    public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } = [];
    public IDownloadClient Create(int id, string name, string settingsJson) => _create();
}

internal sealed class FakeDownloadClient : IDownloadClient
{
    private readonly DownloadClientTestResult? _result;
    private readonly Exception? _throws;
    public FakeDownloadClient(DownloadClientTestResult result) { _result = result; }
    public FakeDownloadClient(Exception throws) { _throws = throws; }
    public int Id => 0;
    public string Name => "fake";
    public DownloadProtocol Protocol => DownloadProtocol.Torrent;
    public Task<DownloadClientItemId> DownloadAsync(RemoteRelease release, CancellationToken ct) =>
        Task.FromResult(new DownloadClientItemId("fake"));
    public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DownloadClientItem>>([]);
    public Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct) => Task.CompletedTask;
    public Task<DownloadClientTestResult> TestAsync(CancellationToken ct) =>
        _throws is not null ? Task.FromException<DownloadClientTestResult>(_throws) : Task.FromResult(_result!);
}
