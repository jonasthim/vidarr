using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.Indexers;

namespace Vidarr.Indexers.Tests;

public class ReleaseSearchServiceTests
{
    private static IndexerSearchCriteria SampleCriteria() => new("query", "Daft Punk", "Around the World", 1997, []);

    private static ReleaseInfo Sample(string title, string guid, string indexerName, DownloadProtocol p = DownloadProtocol.Usenet, string url = "https://example.com/r") =>
        new(title, new Uri(url), null, 100, DateTimeOffset.UtcNow, TimeSpan.Zero, null, null, p, indexerName, "6030",
            string.IsNullOrEmpty(guid) ? new Dictionary<string, string>() : new Dictionary<string, string> { ["guid"] = guid });

    [Fact]
    public async Task Search_fans_out_across_indexers_and_aggregates()
    {
        var a = new StubIndexer(1, "A", [Sample("R1", "g1", "A"), Sample("R2", "g2", "A")]);
        var b = new StubIndexer(2, "B", [Sample("R3", "g3", "B")]);
        var sut = new ReleaseSearchService([a, b], NullLogger<ReleaseSearchService>.Instance);

        var result = await sut.SearchAsync(SampleCriteria(), default);

        result.Releases.Should().HaveCount(3);
        result.Failures.Should().BeEmpty();
        result.IndexersQueried.Should().Be(2);
    }

    [Fact]
    public async Task Dedupes_by_guid()
    {
        var a = new StubIndexer(1, "A", [Sample("R1", "shared", "A"), Sample("R2", "g2", "A")]);
        var b = new StubIndexer(2, "B", [Sample("R1", "shared", "B")]);
        var sut = new ReleaseSearchService([a, b], NullLogger<ReleaseSearchService>.Instance);

        var result = await sut.SearchAsync(SampleCriteria(), default);
        result.Releases.Should().HaveCount(2);
    }

    [Fact]
    public async Task Dedupes_by_title_indexer_url_when_no_guid()
    {
        var a = new StubIndexer(1, "A", [Sample("Same", "", "A", url: "https://x.example/r"), Sample("Same", "", "A", url: "https://x.example/r")]);
        var sut = new ReleaseSearchService([a], NullLogger<ReleaseSearchService>.Instance);
        var result = await sut.SearchAsync(SampleCriteria(), default);
        result.Releases.Should().ContainSingle();
    }

    [Fact]
    public async Task Failing_indexer_does_not_kill_search()
    {
        var good = new StubIndexer(1, "Good", [Sample("R1", "g1", "Good")]);
        var bad = new ThrowingIndexer(2, "Bad", new InvalidOperationException("kaboom"));
        var sut = new ReleaseSearchService([good, bad], NullLogger<ReleaseSearchService>.Instance);

        var result = await sut.SearchAsync(SampleCriteria(), default);

        result.Releases.Should().ContainSingle();
        result.Failures.Should().ContainSingle()
            .Which.Reason.Should().Contain("kaboom");
    }

    [Fact]
    public async Task Per_indexer_timeout_is_enforced()
    {
        var slow = new SlowIndexer(1, "Slow", TimeSpan.FromSeconds(5));
        var sut = new ReleaseSearchService([slow], NullLogger<ReleaseSearchService>.Instance, TimeSpan.FromMilliseconds(50));

        var result = await sut.SearchAsync(SampleCriteria(), default);
        result.Failures.Should().ContainSingle()
            .Which.Reason.Should().ContainAny("timed out", "timeout", "canceled");
        result.Releases.Should().BeEmpty();
    }

    [Fact]
    public async Task RssSync_only_calls_indexers_that_support_rss()
    {
        var rssOk = new StubIndexer(1, "RssOk", [Sample("R", "g1", "RssOk")]) { SupportsRssOverride = true };
        var noRss = new StubIndexer(2, "NoRss", [Sample("Z", "g2", "NoRss")]) { SupportsRssOverride = false };
        var sut = new ReleaseSearchService([rssOk, noRss], NullLogger<ReleaseSearchService>.Instance);

        var result = await sut.RssSyncAsync(default);
        result.Releases.Should().ContainSingle().Which.IndexerName.Should().Be("RssOk");
    }

    [Fact]
    public async Task Search_only_calls_indexers_that_support_search()
    {
        var supports = new StubIndexer(1, "S", [Sample("R", "g1", "S")]) { SupportsSearchOverride = true };
        var noSearch = new StubIndexer(2, "N", [Sample("Z", "g2", "N")]) { SupportsSearchOverride = false };
        var sut = new ReleaseSearchService([supports, noSearch], NullLogger<ReleaseSearchService>.Instance);

        var result = await sut.SearchAsync(SampleCriteria(), default);
        result.Releases.Should().ContainSingle().Which.IndexerName.Should().Be("S");
    }

    [Fact]
    public async Task Empty_indexer_set_returns_empty()
    {
        var sut = new ReleaseSearchService([], NullLogger<ReleaseSearchService>.Instance);
        var result = await sut.SearchAsync(SampleCriteria(), default);
        result.Releases.Should().BeEmpty();
        result.IndexersQueried.Should().Be(0);
    }

    private sealed class StubIndexer : IIndexer
    {
        public StubIndexer(int id, string name, IReadOnlyList<ReleaseInfo> results)
        {
            Id = id;
            Name = name;
            Results = results;
        }
        public int Id { get; }
        public string Name { get; }
        public DownloadProtocol Protocol => DownloadProtocol.Usenet;
        public bool SupportsRssOverride { get; set; } = true;
        public bool SupportsSearchOverride { get; set; } = true;
        public bool SupportsRss => SupportsRssOverride;
        public bool SupportsSearch => SupportsSearchOverride;
        public IReadOnlyList<ReleaseInfo> Results { get; }
        public Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria c, CancellationToken ct) => Task.FromResult(Results);
        public Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) => Task.FromResult(Results);
        public Task<IndexerTestResult> TestAsync(CancellationToken ct) => Task.FromResult(new IndexerTestResult(true, null));
    }

    private sealed class ThrowingIndexer : IIndexer
    {
        private readonly Exception _ex;
        public ThrowingIndexer(int id, string name, Exception ex) { Id = id; Name = name; _ex = ex; }
        public int Id { get; }
        public string Name { get; }
        public DownloadProtocol Protocol => DownloadProtocol.Usenet;
        public bool SupportsRss => true;
        public bool SupportsSearch => true;
        public Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria c, CancellationToken ct) => throw _ex;
        public Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) => throw _ex;
        public Task<IndexerTestResult> TestAsync(CancellationToken ct) => throw _ex;
    }

    private sealed class SlowIndexer : IIndexer
    {
        private readonly TimeSpan _delay;
        public SlowIndexer(int id, string name, TimeSpan delay) { Id = id; Name = name; _delay = delay; }
        public int Id { get; }
        public string Name { get; }
        public DownloadProtocol Protocol => DownloadProtocol.Usenet;
        public bool SupportsRss => true;
        public bool SupportsSearch => true;
        public async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria c, CancellationToken ct)
        {
            await Task.Delay(_delay, ct);
            return [];
        }
        public Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) => FetchAsync(SampleCriteria(), ct);
        public Task<IndexerTestResult> TestAsync(CancellationToken ct) => Task.FromResult(new IndexerTestResult(true, null));
    }
}
