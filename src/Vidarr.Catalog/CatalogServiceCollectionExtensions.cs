using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Catalog;

[ExcludeFromCodeCoverage(Justification = "DI composition glue; exercised by integration tests against Vidarr.Host.")]
public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddVidarrCatalog(this IServiceCollection services, string sqliteConnectionString)
    {
        services.AddDbContext<VidarrDbContext>(opts => opts.UseSqlite(sqliteConnectionString));
        services.AddScoped<IArtistRepository, ArtistRepository>();
        services.AddScoped<IMusicVideoRepository, MusicVideoRepository>();
        services.AddScoped<IMusicVideoFileRepository, MusicVideoFileRepository>();
        services.AddScoped<IRootFolderRepository, RootFolderRepository>();
        return services;
    }
}
