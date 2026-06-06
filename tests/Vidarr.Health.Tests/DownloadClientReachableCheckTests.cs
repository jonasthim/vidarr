using Vidarr.Catalog.Entities;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;

namespace Vidarr.Health.Tests;

public class DownloadClientReachableCheckTests
{
    [Fact]
    public async Task Returns_no_issues_when_client_test_succeeds()
    {
        var repo = new FakeDownloadClientConfigRepository();
        await repo.AddAsync(new DownloadClientConfig { Name = "qBit", Implementation = "QBittorrent", Enable = true }, default);
        var factory = new FakeDownloadClientFactory("QBittorrent",
            () => new FakeDownloadClient(new DownloadClientTestResult(true, "OK")));

        var check = new DownloadClientReachableCheck(repo, [factory]);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Raises_warning_when_client_test_fails()
    {
        var repo = new FakeDownloadClientConfigRepository();
        await repo.AddAsync(new DownloadClientConfig { Name = "qBit", Implementation = "QBittorrent", Enable = true }, default);
        var factory = new FakeDownloadClientFactory("QBittorrent",
            () => new FakeDownloadClient(new DownloadClientTestResult(false, "refused")));

        var check = new DownloadClientReachableCheck(repo, [factory]);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Warning);
    }

    [Fact]
    public async Task Skips_disabled_clients()
    {
        var repo = new FakeDownloadClientConfigRepository();
        await repo.AddAsync(new DownloadClientConfig { Name = "qBit", Implementation = "QBittorrent", Enable = false }, default);
        var factory = new FakeDownloadClientFactory("QBittorrent",
            () => new FakeDownloadClient(new DownloadClientTestResult(false, "refused")));

        var check = new DownloadClientReachableCheck(repo, [factory]);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Raises_warning_when_client_throws()
    {
        var repo = new FakeDownloadClientConfigRepository();
        await repo.AddAsync(new DownloadClientConfig { Name = "qBit", Implementation = "QBittorrent", Enable = true }, default);
        var factory = new FakeDownloadClientFactory("QBittorrent",
            () => new FakeDownloadClient(new InvalidOperationException("nope")));

        var check = new DownloadClientReachableCheck(repo, [factory]);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Message.Should().Contain("nope");
    }

    [Fact]
    public async Task Raises_warning_for_unknown_implementation()
    {
        var repo = new FakeDownloadClientConfigRepository();
        await repo.AddAsync(new DownloadClientConfig { Name = "Mystery", Implementation = "Bogus", Enable = true }, default);

        var check = new DownloadClientReachableCheck(repo, []);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Message.Should().Contain("unknown implementation");
    }

    [Fact]
    public void Name_returns_class_name()
    {
        var check = new DownloadClientReachableCheck(new FakeDownloadClientConfigRepository(), []);
        check.Name.Should().Be("DownloadClientReachableCheck");
    }
}
