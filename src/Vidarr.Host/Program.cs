using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Vidarr.Api;
using Vidarr.Catalog;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
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
var backupFolder = Environment.GetEnvironmentVariable("VIDARR_BACKUP_FOLDER") ?? config["Vidarr:Backup:Folder"] ?? "data/backups";
var backupRetention = int.TryParse(config["Vidarr:Backup:Retention"], out var r) ? r : 10;
var appsettingsPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.json");
var sqliteConn = $"Data Source={sqlitePath}";
var imvdbKey = Environment.GetEnvironmentVariable("VIDARR_IMVDB_KEY") ?? config["Vidarr:Imvdb:ApiKey"];
var incompleteFolder = Environment.GetEnvironmentVariable("VIDARR_INCOMPLETE") ?? config["Vidarr:IncompleteFolder"] ?? "data/incomplete";

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlitePath))!);
Directory.CreateDirectory(Path.GetFullPath(incompleteFolder));
Directory.CreateDirectory(Path.GetFullPath(backupFolder));

// Apply any staged restore before EF Core opens the SQLite file.
if (Vidarr.Backup.RestoreBootstrap.ApplyPendingRestore(
        Path.GetFullPath(sqlitePath),
        File.Exists(appsettingsPath) ? appsettingsPath : null))
{
    Log.Information("Applied staged backup restore on startup");
}

builder.Services.AddSingleton(new ApiKeyOptions(apiKey));
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ISessionSigner, HmacSessionSigner>();
builder.Services.AddVidarrInfrastructure();
builder.Services.AddVidarrCatalog(sqliteConn);

builder.Services.AddSingleton(new ImvdbOptions(imvdbKey));
builder.Services.AddSingleton<IMetadataProvider, ImvdbMetadataProvider>();
builder.Services.AddSingleton<IReleaseParser, ReleaseParser>();
builder.Services.AddSingleton<INamingService, NamingService>();
builder.Services.AddSingleton<IImporterService, ImporterService>();

// Phase 10 — chapter-aware importer.
builder.Services.AddSingleton<Vidarr.ChapterSplit.IMediaInspector, Vidarr.ChapterSplit.MediaInspector>();
builder.Services.AddSingleton<Vidarr.ChapterSplit.IChapterSplitter, Vidarr.ChapterSplit.ChapterSplitter>();
builder.Services.AddSingleton<Vidarr.ChapterSplit.IChapterTitleMatcher, Vidarr.ChapterSplit.ChapterTitleMatcher>();
builder.Services.AddSingleton<IChapterAwareImportPipeline, ChapterAwareImportPipeline>();

builder.Services.AddSingleton<IYouTubeQualityMapper, YouTubeQualityMapper>();
builder.Services.AddSingleton(new YouTubeIndexerSettings(ChannelIds: [], MaxResults: 10, RssBatchSize: 15, Timeout: TimeSpan.FromMinutes(2)));
builder.Services.AddSingleton<IIndexer>(sp => new YouTubeIndexer(
    1, "YouTube",
    sp.GetRequiredService<YouTubeIndexerSettings>(),
    sp.GetRequiredService<IProcessRunner>(),
    sp.GetRequiredService<IHttpClient>(),
    sp.GetRequiredService<IYouTubeQualityMapper>()));

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

// Download-client factory registry (Phase 4) — DB-backed configs get materialised at poll time.
builder.Services.AddSingleton<IDownloadClientFactory, QBittorrentFactory>();
builder.Services.AddSingleton<IDownloadClientFactory, TransmissionFactory>();
builder.Services.AddSingleton<IDownloadClientFactory, DelugeFactory>();
builder.Services.AddSingleton<IDownloadClientFactory, SABnzbdFactory>();
builder.Services.AddSingleton<IDownloadClientFactory, NZBGetFactory>();
builder.Services.AddSingleton<IDownloadClientFactory, YtDlpFactory>();
builder.Services.AddScoped<IDownloadClientRegistry, DownloadClientRegistry>();

builder.Services.AddSingleton<IEventBus, InProcessEventBus>();

// Phase 11 — notification factories + dispatcher.
builder.Services.AddSingleton<INotificationFactory, WebhookFactory>();
builder.Services.AddSingleton<INotificationFactory, PlexFactory>();
builder.Services.AddSingleton<INotificationFactory, JellyfinFactory>();
builder.Services.AddSingleton<INotificationFactory, DiscordFactory>();
builder.Services.AddSingleton<NotificationDispatcher>(sp =>
{
    var bus = sp.GetRequiredService<IEventBus>();
    var logger = sp.GetRequiredService<ILogger<NotificationDispatcher>>();
    var factories = sp.GetServices<INotificationFactory>().ToDictionary(f => f.Implementation, StringComparer.OrdinalIgnoreCase);
    var notifierFactory = new Func<IReadOnlyList<INotification>>(() =>
    {
        using var scope = sp.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<INotificationConfigRepository>();
        var configs = repo.ListAsync(default).GetAwaiter().GetResult();
        var notifiers = new List<INotification>();
        foreach (var c in configs.Where(x => x.Enable))
        {
            if (!factories.TryGetValue(c.Implementation, out var factory)) continue;
            try
            {
                var events = JsonSerializer.Deserialize<int[]>(c.SubscribedEventsJson) ?? [];
                var set = events.Select(e => (NotificationEventType)e).ToHashSet();
                notifiers.Add(factory.Create(c.Id, c.Name, c.SettingsJson, set));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to materialise notification {Name}", c.Name);
            }
        }
        return notifiers;
    });
    return new NotificationDispatcher(bus, notifierFactory, logger);
});
builder.Services.AddHostedService<NotificationDispatcherHostedService>();
builder.Services.AddSingleton<ICommandQueue, ChannelCommandQueue>();
builder.Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
builder.Services.AddHostedService<CommandWorker>();

builder.Services.AddScoped<ICommandHandler<ArtistSearchCommand>, ArtistSearchCommandHandler>();

// Phase 9 — Discovery rules engine.
builder.Services.AddScoped<Vidarr.Rules.IDiscoveryRuleEngine, Vidarr.Rules.DiscoveryRuleEngine>();

// Phase 7 — Recurring jobs + history.
builder.Services.AddSingleton<IJobRunHistory, InMemoryJobRunHistory>();
builder.Services.AddSingleton<IRecurringJobRunner, RecurringJobRunner>();
builder.Services.AddSingleton<IRecurringJob, Vidarr.Host.Jobs.ArtistRefreshJob>();
builder.Services.AddSingleton<IRecurringJob, Vidarr.Host.Jobs.RssSyncJob>();
builder.Services.AddSingleton<IRecurringJob, Vidarr.Host.Jobs.DownloadStatusPollJob>();
builder.Services.AddSingleton<IRecurringJob, Vidarr.Host.Jobs.WantedVideoSearchJob>();
builder.Services.AddSingleton<IRecurringJob, Vidarr.Host.Jobs.RuleSetEvaluationJob>();
builder.Services.AddSingleton<IRecurringJob, Vidarr.Host.Jobs.BackupJob>();

// Phase 13 — Backup pipeline.
builder.Services.AddSingleton(new Vidarr.Backup.BackupOptions(
    BackupFolder: Path.GetFullPath(backupFolder),
    SqliteSourcePath: Path.GetFullPath(sqlitePath),
    ConfigSourcePath: File.Exists(appsettingsPath) ? appsettingsPath : null,
    RetentionCount: backupRetention));
builder.Services.AddScoped<Vidarr.Backup.IDbCheckpointer, Vidarr.Backup.SqliteWalCheckpointer>();
builder.Services.AddScoped<Vidarr.Backup.IBackupService, Vidarr.Backup.BackupService>();

// Phase 12 — Health monitor + checks.
builder.Services.AddScoped<Vidarr.Health.IHealthCheck, Vidarr.Health.DiskSpaceCheck>();
builder.Services.AddScoped<Vidarr.Health.IHealthCheck, Vidarr.Health.RootFolderAccessibleCheck>();
builder.Services.AddScoped<Vidarr.Health.IHealthCheck, Vidarr.Health.IndexerReachableCheck>();
builder.Services.AddScoped<Vidarr.Health.IHealthCheck, Vidarr.Health.DownloadClientReachableCheck>();
builder.Services.AddScoped<Vidarr.Health.IHealthCheck, Vidarr.Health.YtDlpVersionCheck>();
builder.Services.AddSingleton(sp =>
{
    var cfg = sp.GetServices<Vidarr.Catalog.Repositories.IApplicationConfigRepository>().FirstOrDefault();
    // Configuration drives the actual binary path; the updater itself only reads at runtime.
    return new Vidarr.Health.YtDlpUpdaterOptions(
        BinaryPath: Environment.GetEnvironmentVariable("VIDARR_YTDLP_PATH") ?? config["Vidarr:YtDlp:Path"] ?? "yt-dlp");
});
builder.Services.AddSingleton<Vidarr.Health.IYtDlpUpdater, Vidarr.Health.YtDlpUpdater>();
builder.Services.AddScoped<IRecurringJob, Vidarr.Host.Jobs.YtDlpUpdaterJob>();

builder.Services.AddSingleton<Vidarr.Health.IHealthMonitor>(sp => new Vidarr.Health.HealthMonitor(
    new HealthCheckResolver(sp),
    sp.GetRequiredService<IEventBus>(),
    sp.GetRequiredService<ILogger<Vidarr.Health.HealthMonitor>>()));
builder.Services.AddSingleton<IRecurringJob, Vidarr.Host.Jobs.HealthCheckJob>();
builder.Services.AddHostedService<RecurringJobsHostedService>();

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
app.MapVidarrDownloadClientApi();
app.MapVidarrSystemCommandApi();
app.MapVidarrDiscoveryRuleApi();
app.MapVidarrNotificationApi();
app.MapVidarrHealthApi();
app.MapVidarrAuthApi();
app.MapVidarrBackupApi();

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
