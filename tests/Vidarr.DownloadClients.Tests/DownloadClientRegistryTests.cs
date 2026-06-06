using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;

namespace Vidarr.DownloadClients.Tests;

public class DownloadClientRegistryTests
{
    [Fact]
    public async Task GetActive_skips_disabled_configs()
    {
        var repo = Substitute.For<IDownloadClientConfigRepository>();
        repo.ListAsync(default).Returns(Task.FromResult<IReadOnlyList<DownloadClientConfig>>(
        [
            new() { Id = 1, Name = "On", Implementation = "Stub", Enable = true, SettingsJson = "{}" },
            new() { Id = 2, Name = "Off", Implementation = "Stub", Enable = false, SettingsJson = "{}" },
        ]));

        var factory = new StubFactory();
        var sut = new DownloadClientRegistry(repo, [factory], NullLogger<DownloadClientRegistry>.Instance);

        var active = await sut.GetActiveAsync(default);
        active.Should().ContainSingle().Which.Name.Should().Be("On");
    }

    [Fact]
    public async Task GetActive_skips_unknown_implementations()
    {
        var repo = Substitute.For<IDownloadClientConfigRepository>();
        repo.ListAsync(default).Returns(Task.FromResult<IReadOnlyList<DownloadClientConfig>>(
        [
            new() { Id = 1, Name = "Known", Implementation = "Stub", Enable = true, SettingsJson = "{}" },
            new() { Id = 2, Name = "Unknown", Implementation = "Mystery", Enable = true, SettingsJson = "{}" },
        ]));

        var sut = new DownloadClientRegistry(repo, [new StubFactory()], NullLogger<DownloadClientRegistry>.Instance);
        (await sut.GetActiveAsync(default)).Should().ContainSingle().Which.Name.Should().Be("Known");
    }

    [Fact]
    public async Task GetActive_skips_factory_throws_and_keeps_going()
    {
        var repo = Substitute.For<IDownloadClientConfigRepository>();
        repo.ListAsync(default).Returns(Task.FromResult<IReadOnlyList<DownloadClientConfig>>(
        [
            new() { Id = 1, Name = "Broken", Implementation = "Bad", Enable = true, SettingsJson = "{}" },
            new() { Id = 2, Name = "Good", Implementation = "Stub", Enable = true, SettingsJson = "{}" },
        ]));

        var sut = new DownloadClientRegistry(
            repo,
            [new StubFactory(), new ThrowingFactory()],
            NullLogger<DownloadClientRegistry>.Instance);

        var active = await sut.GetActiveAsync(default);
        active.Should().ContainSingle().Which.Name.Should().Be("Good");
    }

    private sealed class StubFactory : IDownloadClientFactory
    {
        public string Implementation => "Stub";
        public string DisplayName => "Stub";
        public DownloadProtocol Protocol => DownloadProtocol.Torrent;
        public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } = [];
        public IDownloadClient Create(int id, string name, string settingsJson) => new StubClient(id, name);
    }

    private sealed class ThrowingFactory : IDownloadClientFactory
    {
        public string Implementation => "Bad";
        public string DisplayName => "Bad";
        public DownloadProtocol Protocol => DownloadProtocol.Torrent;
        public IReadOnlyList<DownloadClientFieldSchema> SettingsSchema { get; } = [];
        public IDownloadClient Create(int id, string name, string settingsJson) => throw new InvalidOperationException("nope");
    }

    private sealed class StubClient : IDownloadClient
    {
        public StubClient(int id, string name) { Id = id; Name = name; }
        public int Id { get; }
        public string Name { get; }
        public DownloadProtocol Protocol => DownloadProtocol.Torrent;
        public Task<DownloadClientItemId> DownloadAsync(RemoteRelease release, CancellationToken ct) => Task.FromResult(new DownloadClientItemId("x"));
        public Task<IReadOnlyList<DownloadClientItem>> GetItemsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DownloadClientItem>>([]);
        public Task RemoveAsync(DownloadClientItemId id, bool deleteData, CancellationToken ct) => Task.CompletedTask;
        public Task<DownloadClientTestResult> TestAsync(CancellationToken ct) => Task.FromResult(new DownloadClientTestResult(true, "ok"));
    }
}
