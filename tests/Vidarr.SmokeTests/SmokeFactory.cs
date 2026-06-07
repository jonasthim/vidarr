using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vidarr.Contracts.Abstractions;
using Vidarr.Tests.Common;

namespace Vidarr.SmokeTests;

/// <summary>
/// Boots Vidarr.Host inside the process and swaps every external boundary for a deterministic
/// fake so the smoke tests can assert the full vertical: REST endpoints, DI graph, scheduler,
/// catalog persistence and download-client invocation.
/// </summary>
public sealed class SmokeFactory : WebApplicationFactory<Vidarr.Host.Program>
{
    public FakeHttpClient HttpClient { get; } = new();
    public FakeProcessRunner ProcessRunner { get; } = new();
    public FakeFileSystem FileSystem { get; } = new();
    public FakeClock Clock { get; } = new();

    public SmokeFactory()
    {
        Environment.SetEnvironmentVariable("VIDARR_API_KEY", "smoke-key");
        Environment.SetEnvironmentVariable("VIDARR_SQLITE_PATH",
            Path.Combine(Path.GetTempPath(), $"vidarr-smoke-{Guid.NewGuid():N}.db"));
        Environment.SetEnvironmentVariable("VIDARR_INCOMPLETE",
            Path.Combine(Path.GetTempPath(), $"vidarr-smoke-incomplete-{Guid.NewGuid():N}"));
        Environment.SetEnvironmentVariable("VIDARR_BACKUP_FOLDER",
            Path.Combine(Path.GetTempPath(), $"vidarr-smoke-backups-{Guid.NewGuid():N}"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Smoke");
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
        foreach (var d in descriptors) services.Remove(d);
        services.AddSingleton(typeof(T), impl);
    }
}
