using Microsoft.Extensions.DependencyInjection;
using Vidarr.Contracts.Abstractions;

namespace Vidarr.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddVidarrInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient("vidarr");
        services.AddSingleton<IHttpClient, HttpClientAdapter>();
        services.AddSingleton<IFileSystem, FileSystemAdapter>();
        services.AddSingleton<IProcessRunner, ProcessRunnerAdapter>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IRandom, RandomAdapter>();
        services.AddSingleton<IEnvironment, EnvironmentAdapter>();
        return services;
    }
}
