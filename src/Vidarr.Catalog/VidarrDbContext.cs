using Microsoft.EntityFrameworkCore;
using Vidarr.Catalog.Entities;

namespace Vidarr.Catalog;

public sealed class VidarrDbContext : DbContext
{
    public VidarrDbContext(DbContextOptions<VidarrDbContext> options) : base(options)
    {
    }

    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<MusicVideo> MusicVideos => Set<MusicVideo>();
    public DbSet<MusicVideoFile> MusicVideoFiles => Set<MusicVideoFile>();
    public DbSet<RootFolder> RootFolders => Set<RootFolder>();

    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<QualityProfile> QualityProfiles => Set<QualityProfile>();
    public DbSet<CustomFormat> CustomFormats => Set<CustomFormat>();
    public DbSet<BlocklistEntry> Blocklist => Set<BlocklistEntry>();
    public DbSet<HistoryEvent> History => Set<HistoryEvent>();
    public DbSet<IndexerConfig> Indexers => Set<IndexerConfig>();
    public DbSet<DownloadClientConfig> DownloadClients => Set<DownloadClientConfig>();
    public DbSet<NotificationConfig> Notifications => Set<NotificationConfig>();
    public DbSet<DiscoveryRuleSet> DiscoveryRuleSets => Set<DiscoveryRuleSet>();
    public DbSet<ApplicationConfig> ApplicationConfigs => Set<ApplicationConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite has no native DateTimeOffset → ORDER BY support. Store as TICKS so we can
        // order/filter server-side. Applies to every DateTimeOffset property in the model.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTimeOffset))
                {
                    prop.SetValueConverter(DateTimeOffsetToTicksConverter.Instance);
                }
                else if (prop.ClrType == typeof(DateTimeOffset?))
                {
                    prop.SetValueConverter(NullableDateTimeOffsetToTicksConverter.Instance);
                }
            }
        }

        modelBuilder.Entity<Artist>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SortName);
            e.Property(x => x.Name).IsRequired().HasMaxLength(512);
            e.Property(x => x.SortName).IsRequired().HasMaxLength(512);
            e.Property(x => x.RootFolderPath).HasMaxLength(2048);
            e.HasMany(x => x.MusicVideos).WithOne(x => x.Artist!).HasForeignKey(x => x.ArtistId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MusicVideo>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ArtistId, x.Title });
            e.Property(x => x.Title).IsRequired().HasMaxLength(1024);
            e.HasOne(x => x.File).WithOne(x => x.MusicVideo!).HasForeignKey<MusicVideoFile>(x => x.MusicVideoId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MusicVideoFile>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.MusicVideoId).IsUnique();
            e.Property(x => x.RelativePath).IsRequired().HasMaxLength(2048);
        });

        modelBuilder.Entity<RootFolder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Path).IsUnique();
            e.Property(x => x.Path).IsRequired().HasMaxLength(2048);
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Label).IsUnique();
            e.Property(x => x.Label).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<QualityProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<CustomFormat>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<BlocklistEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ReleaseTitle);
            e.Property(x => x.ReleaseTitle).IsRequired().HasMaxLength(2048);
            e.Property(x => x.IndexerName).HasMaxLength(256);
        });

        modelBuilder.Entity<HistoryEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.ArtistId, x.MusicVideoId });
        });

        modelBuilder.Entity<IndexerConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.Implementation).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<DownloadClientConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.Implementation).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<NotificationConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.Implementation).IsRequired().HasMaxLength(128);
        });

        modelBuilder.Entity<DiscoveryRuleSet>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
        });

        modelBuilder.Entity<ApplicationConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.InstanceName).IsRequired().HasMaxLength(128);
        });
    }
}
