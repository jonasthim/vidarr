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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
