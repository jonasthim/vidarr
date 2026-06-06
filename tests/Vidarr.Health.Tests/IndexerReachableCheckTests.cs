using Vidarr.Catalog.Entities;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;

namespace Vidarr.Health.Tests;

public class IndexerReachableCheckTests
{
    [Fact]
    public async Task Returns_no_issues_when_indexer_test_succeeds()
    {
        var repo = new FakeIndexerConfigRepository();
        await repo.AddAsync(new IndexerConfig { Name = "Geek", Implementation = "Newznab" }, default);
        var factory = new FakeIndexerFactory("Newznab", () => new FakeIndexer(new IndexerTestResult(true, "OK")));

        var check = new IndexerReachableCheck(repo, [factory]);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Raises_warning_when_indexer_test_fails()
    {
        var repo = new FakeIndexerConfigRepository();
        await repo.AddAsync(new IndexerConfig { Name = "Geek", Implementation = "Newznab" }, default);
        var factory = new FakeIndexerFactory("Newznab", () => new FakeIndexer(new IndexerTestResult(false, "401 unauthorized")));

        var check = new IndexerReachableCheck(repo, [factory]);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Warning);
        issues[0].Message.Should().Contain("401 unauthorized");
    }

    [Fact]
    public async Task Raises_warning_when_indexer_throws()
    {
        var repo = new FakeIndexerConfigRepository();
        await repo.AddAsync(new IndexerConfig { Name = "Geek", Implementation = "Newznab" }, default);
        var factory = new FakeIndexerFactory("Newznab", () => new FakeIndexer(new InvalidOperationException("boom")));

        var check = new IndexerReachableCheck(repo, [factory]);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Warning);
        issues[0].Message.Should().Contain("boom");
    }

    [Fact]
    public async Task Raises_warning_for_unknown_implementation()
    {
        var repo = new FakeIndexerConfigRepository();
        await repo.AddAsync(new IndexerConfig { Name = "Mystery", Implementation = "Bogus" }, default);

        var check = new IndexerReachableCheck(repo, []);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Message.Should().Contain("unknown implementation");
    }

    [Fact]
    public void Name_returns_class_name()
    {
        var check = new IndexerReachableCheck(new FakeIndexerConfigRepository(), []);
        check.Name.Should().Be("IndexerReachableCheck");
    }
}
