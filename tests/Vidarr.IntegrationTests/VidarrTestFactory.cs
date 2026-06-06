using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vidarr.Contracts.Abstractions;
using Vidarr.Tests.Common;

namespace Vidarr.IntegrationTests;

/// <summary>
/// Boots the full Vidarr.Host pipeline (DI + EF Core SQLite + middleware + endpoints + scheduler)
/// but replaces every external boundary (HTTP, processes, file system, clock) with a deterministic fake.
/// </summary>
public sealed class VidarrTestFactory : WebApplicationFactory<Vidarr.Host.Program>
{
    public FakeHttpClient HttpClient { get; } = new();
    public FakeProcessRunner ProcessRunner { get; } = new();
    public FakeFileSystem FileSystem { get; } = new();
    public FakeClock Clock { get; } = new();

    public VidarrTestFactory()
    {
        Environment.SetEnvironmentVariable("VIDARR_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("VIDARR_SQLITE_PATH",
            Path.Combine(Path.GetTempPath(), $"vidarr-it-{Guid.NewGuid():N}.db"));
        Environment.SetEnvironmentVariable("VIDARR_INCOMPLETE",
            Path.Combine(Path.GetTempPath(), $"vidarr-it-{Guid.NewGuid():N}"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            ReplaceSingleton<IHttpClient>(services, HttpClient);
            ReplaceSingleton<IProcessRunner>(services, ProcessRunner);
            ReplaceSingleton<IFileSystem>(services, FileSystem);
            ReplaceSingleton<ISystemClock>(services, Clock);
        });
    }

    private static void ReplaceSingleton<T>(IServiceCollection services, object impl) where T : class
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var d in descriptors)
        {
            services.Remove(d);
        }
        services.AddSingleton(typeof(T), impl);
    }
}
