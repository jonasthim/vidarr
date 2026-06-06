using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Vidarr.Api;
using Vidarr.Catalog;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Decision;
using Vidarr.DownloadClients;
using Vidarr.EventBus;
using Vidarr.Host;
using Vidarr.Importer;
using Vidarr.Indexers;
using Vidarr.Infrastructure;
using Vidarr.Metadata;
using Vidarr.Naming;
using Vidarr.Notifications;
using Vidarr.Scheduler;

[assembly: ExcludeFromCodeCoverage(Justification = "Composition root; exercised by integration tests against the running app.")]

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(ctx.HostingEnvironment.ContentRootPath, "data", "logs", "vidarr-.log"), rollingInterval: RollingInterval.Day));

var config = builder.Configuration;
var apiKey = Environment.GetEnvironmentVariable("VIDARR_API_KEY") ?? config["Vidarr:ApiKey"] ?? Guid.NewGuid().ToString("N");
var sqlitePath = Environment.GetEnvironmentVariable("VIDARR_SQLITE_PATH") ?? config["Vidarr:Sqlite:Path"] ?? "data/vidarr.db";
var sqliteConn = $"Data Source={sqlitePath}";
var imvdbKey = Environment.GetEnvironmentVariable("VIDARR_IMVDB_KEY") ?? config["Vidarr:Imvdb:ApiKey"];
var incompleteFolder = Environment.GetEnvironmentVariable("VIDARR_INCOMPLETE") ?? config["Vidarr:IncompleteFolder"] ?? "data/incomplete";

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!);
Directory.CreateDirectory(Path.GetFullPath(incompleteFolder));

builder.Services.AddSingleton(new ApiKeyOptions(apiKey));
builder.Services.AddVidarrInfrastructure();
builder.Services.AddVidarrCatalog(sqliteConn);

builder.Services.AddSingleton(new ImvdbOptions(imvdbKey));
builder.Services.AddSingleton<IMetadataProvider, ImvdbMetadataProvider>();
builder.Services.AddSingleton<IReleaseParser, ReleaseParser>();
builder.Services.AddSingleton<INamingService, NamingService>();
builder.Services.AddSingleton<IImporterService, ImporterService>();

builder.Services.AddSingleton(new YouTubeIndexerSettings(ChannelIds: [], MaxResults: 10, RssBatchSize: 15, Timeout: TimeSpan.FromMinutes(2)));
builder.Services.AddSingleton<IIndexer>(sp => new YouTubeIndexer(
    1, "YouTube", sp.GetRequiredService<YouTubeIndexerSettings>(), sp.GetRequiredService<IProcessRunner>()));

// Indexer factories — Phase 3 plug-in registry used by REST /indexer/schema + /indexer/test.
builder.Services.AddSingleton<IIndexerFactory, NewznabIndexerFactory>();
builder.Services.AddSingleton<IIndexerFactory, TorznabIndexerFactory>();
builder.Services.AddSingleton<IIndexerFactory, YouTubeIndexerFactory>();
builder.Services.AddSingleton<IReleaseSearchService, ReleaseSearchService>();

builder.Services.AddSingleton(new YtDlpDownloadClientSettings(IncompleteFolder: incompleteFolder, Timeout: TimeSpan.FromHours(1)));
builder.Services.AddSingleton<IDownloadClient>(sp => new YtDlpDownloadClient(
    1, "yt-dlp",
    sp.GetRequiredService<YtDlpDownloadClientSettings>(),
    sp.GetRequiredService<IProcessRunner>(),
    sp.GetRequiredService<IFileSystem>()));

builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
builder.Services.AddSingleton<ICommandQueue, ChannelCommandQueue>();
builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddHostedService<CommandWorker>();

builder.Services.AddScoped<ICommandHandler<ArtistSearchCommand>, ArtistSearchCommandHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VidarrDbContext>();
    await db.Database.EnsureCreatedAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<Vidarr.Catalog.Seeding.IDataSeeder>();
    await seeder.SeedAsync(db, default);
}

app.UseApiKeyAuth(new ApiKeyOptions(apiKey));
app.MapVidarrApi();
app.MapVidarrSettingsApi();
app.MapVidarrReleaseApi();

var wwwroot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(wwwroot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

Log.Information("Vidarr starting on {Url}", string.Join(",", app.Urls.Count > 0 ? app.Urls : ["default"]));
Log.Information("Vidarr API key: {Key}", apiKey);

await app.RunAsync();

namespace Vidarr.Host
{
    public partial class Program
    {
        // Made public/partial for WebApplicationFactory<Program> in integration tests.
    }
}
