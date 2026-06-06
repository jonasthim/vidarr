using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vidarr.Catalog.Repositories;
using Vidarr.Catalog.Seeding;

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
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IQualityProfileRepository, QualityProfileRepository>();
        services.AddScoped<ICustomFormatRepository, CustomFormatRepository>();
        services.AddScoped<IBlocklistRepository, BlocklistRepository>();
        services.AddScoped<IHistoryRepository, HistoryRepository>();
        services.AddScoped<IIndexerConfigRepository, IndexerConfigRepository>();
        services.AddScoped<IDownloadClientConfigRepository, DownloadClientConfigRepository>();
        services.AddScoped<INotificationConfigRepository, NotificationConfigRepository>();
        services.AddScoped<IDiscoveryRuleSetRepository, DiscoveryRuleSetRepository>();
        services.AddScoped<IApplicationConfigRepository, ApplicationConfigRepository>();
        services.AddSingleton<IDataSeeder, DataSeeder>();
        return services;
    }
}
