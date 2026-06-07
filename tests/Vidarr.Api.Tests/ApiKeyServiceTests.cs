using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Api;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Api.Tests;

public class ApiKeyServiceTests
{
    private static (ApiKeyService Svc, InMemoryConfigRepo Repo) NewService(string? overrideValue = null)
    {
        var repo = new InMemoryConfigRepo();
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationConfigRepository>(repo);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var svc = new ApiKeyService(new ApiKeyOverride(overrideValue), scopeFactory, NullLogger<ApiKeyService>.Instance);
        return (svc, repo);
    }

    [Fact]
    public async Task Returns_override_when_set_and_refuses_rotate()
    {
        var (svc, _) = NewService("fixed-key");
        (await svc.GetCurrentAsync(default)).Should().Be("fixed-key");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RotateAsync(default));
    }

    [Fact]
    public async Task Generates_and_persists_key_on_first_read()
    {
        var (svc, repo) = NewService();
        var first = await svc.GetCurrentAsync(default);
        first.Should().HaveLength(32);
        repo.Stored!.ApiKey.Should().Be(first);
    }

    [Fact]
    public async Task Caches_after_first_read()
    {
        var (svc, repo) = NewService();
        var a = await svc.GetCurrentAsync(default);
        var b = await svc.GetCurrentAsync(default);
        a.Should().Be(b);
        repo.UpdateCalls.Should().Be(1);
    }

    [Fact]
    public async Task Rotate_persists_new_key_and_updates_cache()
    {
        var (svc, repo) = NewService();
        var initial = await svc.GetCurrentAsync(default);
        var rotated = await svc.RotateAsync(default);

        rotated.Should().NotBe(initial);
        repo.Stored!.ApiKey.Should().Be(rotated);
        (await svc.GetCurrentAsync(default)).Should().Be(rotated);
    }

    [Fact]
    public async Task Returns_existing_db_value_without_regenerating()
    {
        var (svc, repo) = NewService();
        repo.Stored = new ApplicationConfig { ApiKey = "persisted-from-prior-boot" };
        (await svc.GetCurrentAsync(default)).Should().Be("persisted-from-prior-boot");
        repo.UpdateCalls.Should().Be(0);
    }

    private sealed class InMemoryConfigRepo : IApplicationConfigRepository
    {
        public ApplicationConfig? Stored { get; set; }
        public int UpdateCalls;
        public Task<ApplicationConfig> GetAsync(CancellationToken ct)
        {
            Stored ??= new ApplicationConfig();
            return Task.FromResult(Stored);
        }
        public Task UpdateAsync(ApplicationConfig config, CancellationToken ct)
        {
            Stored = config;
            UpdateCalls++;
            return Task.CompletedTask;
        }
    }
}
