using Microsoft.EntityFrameworkCore;
using Vidarr.Catalog.Entities;

namespace Vidarr.Catalog.Repositories;

public interface IArtistRepository
{
    Task<Artist?> GetAsync(int id, CancellationToken ct);
    Task<Artist?> FindByExternalIdAsync(string providerKey, string providerId, CancellationToken ct);
    Task<IReadOnlyList<Artist>> ListAsync(CancellationToken ct);
    Task<Artist> AddAsync(Artist artist, CancellationToken ct);
    Task UpdateAsync(Artist artist, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public interface IMusicVideoRepository
{
    Task<MusicVideo?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<MusicVideo>> ListByArtistAsync(int artistId, CancellationToken ct);
    Task<IReadOnlyList<MusicVideo>> ListWantedAsync(CancellationToken ct);
    /// <summary>Monitored videos that have a file whose quality is below the artist's profile cutoff.</summary>
    Task<IReadOnlyList<MusicVideo>> ListCutoffUnmetAsync(CancellationToken ct);
    /// <summary>Videos whose ReleaseDate (or Year, if no exact date) falls in [from, to]. Both inclusive.</summary>
    Task<IReadOnlyList<MusicVideo>> ListByReleaseRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<MusicVideo> AddAsync(MusicVideo video, CancellationToken ct);
    Task UpdateAsync(MusicVideo video, CancellationToken ct);
}

public interface IMusicVideoFileRepository
{
    Task<MusicVideoFile?> GetAsync(int id, CancellationToken ct);
    Task<MusicVideoFile?> GetByMusicVideoIdAsync(int musicVideoId, CancellationToken ct);
    Task<MusicVideoFile> AddAsync(MusicVideoFile file, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public interface IRootFolderRepository
{
    Task<RootFolder?> GetAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<RootFolder>> ListAsync(CancellationToken ct);
    Task<RootFolder> AddAsync(RootFolder folder, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class ArtistRepository(VidarrDbContext db) : IArtistRepository
{
    public Task<Artist?> GetAsync(int id, CancellationToken ct) =>
        db.Artists.Include(a => a.MusicVideos).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Artist?> FindByExternalIdAsync(string providerKey, string providerId, CancellationToken ct)
    {
        var needle = $"\"{providerKey}\":\"{providerId}\"";
        return db.Artists.FirstOrDefaultAsync(a => a.ExternalIdsJson.Contains(needle), ct);
    }

    public async Task<IReadOnlyList<Artist>> ListAsync(CancellationToken ct) =>
        await db.Artists.OrderBy(a => a.SortName).ToListAsync(ct);

    public async Task<Artist> AddAsync(Artist artist, CancellationToken ct)
    {
        db.Artists.Add(artist);
        await db.SaveChangesAsync(ct);
        return artist;
    }

    public Task UpdateAsync(Artist artist, CancellationToken ct)
    {
        db.Artists.Update(artist);
        return db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.Artists.FindAsync([id], ct);
        if (existing is not null)
        {
            db.Artists.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public sealed class MusicVideoRepository(VidarrDbContext db) : IMusicVideoRepository
{
    public Task<MusicVideo?> GetAsync(int id, CancellationToken ct) =>
        db.MusicVideos.Include(v => v.File).FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<MusicVideo>> ListByArtistAsync(int artistId, CancellationToken ct) =>
        await db.MusicVideos.Where(v => v.ArtistId == artistId).OrderBy(v => v.Year).ThenBy(v => v.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<MusicVideo>> ListWantedAsync(CancellationToken ct) =>
        await db.MusicVideos
            .Include(v => v.Artist)
            .Where(v => v.Monitored && !v.HasFile)
            .OrderBy(v => v.ArtistId)
            .ThenBy(v => v.Year)
            .ThenBy(v => v.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MusicVideo>> ListCutoffUnmetAsync(CancellationToken ct)
    {
        // For each monitored downloaded video, the file.QualityId must be >= the artist's
        // profile cutoff. We join through the relational shape directly so EF Core
        // translates this to a single SQL statement.
        var query =
            from v in db.MusicVideos
            join f in db.MusicVideoFiles on v.FileId equals f.Id
            join a in db.Artists on v.ArtistId equals a.Id
            join p in db.QualityProfiles on a.QualityProfileId equals p.Id
            where v.Monitored && v.HasFile && f.QualityId < p.CutoffQualityId
            select v;
        return await query
            .Include(v => v.File)
            .Include(v => v.Artist)
            .OrderBy(v => v.ArtistId)
            .ThenBy(v => v.Year)
            .ThenBy(v => v.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MusicVideo>> ListByReleaseRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        // Year acts as a coarse "premiere date" when no exact ReleaseDate is recorded —
        // surfacing year-only entries lets the Calendar show legacy/back-catalog videos.
        var fromYear = from.Year;
        var toYear = to.Year;
        var rows = await db.MusicVideos
            .Include(v => v.Artist)
            .Where(v =>
                (v.ReleaseDate != null && v.ReleaseDate >= from && v.ReleaseDate <= to)
                || (v.ReleaseDate == null && v.Year != null && v.Year >= fromYear && v.Year <= toYear))
            .ToListAsync(ct);
        // Sort in memory: EF Core can't translate the year-fallback DateTimeOffset
        // ctor to SQL. The result set for a calendar query is bounded by month/year,
        // so post-processing is cheap.
        return [.. rows.OrderBy(v =>
                v.ReleaseDate ?? new DateTimeOffset(v.Year!.Value, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .ThenBy(v => v.Title)];
    }

    public async Task<MusicVideo> AddAsync(MusicVideo video, CancellationToken ct)
    {
        db.MusicVideos.Add(video);
        await db.SaveChangesAsync(ct);
        return video;
    }

    public Task UpdateAsync(MusicVideo video, CancellationToken ct)
    {
        db.MusicVideos.Update(video);
        return db.SaveChangesAsync(ct);
    }
}

public sealed class MusicVideoFileRepository(VidarrDbContext db) : IMusicVideoFileRepository
{
    public Task<MusicVideoFile?> GetAsync(int id, CancellationToken ct) =>
        db.MusicVideoFiles.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<MusicVideoFile?> GetByMusicVideoIdAsync(int musicVideoId, CancellationToken ct) =>
        db.MusicVideoFiles.FirstOrDefaultAsync(f => f.MusicVideoId == musicVideoId, ct);

    public async Task<MusicVideoFile> AddAsync(MusicVideoFile file, CancellationToken ct)
    {
        db.MusicVideoFiles.Add(file);
        await db.SaveChangesAsync(ct);
        return file;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.MusicVideoFiles.FindAsync([id], ct);
        if (existing is not null)
        {
            db.MusicVideoFiles.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public sealed class RootFolderRepository(VidarrDbContext db) : IRootFolderRepository
{
    public Task<RootFolder?> GetAsync(int id, CancellationToken ct) =>
        db.RootFolders.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<RootFolder>> ListAsync(CancellationToken ct) =>
        await db.RootFolders.OrderBy(f => f.Path).ToListAsync(ct);

    public async Task<RootFolder> AddAsync(RootFolder folder, CancellationToken ct)
    {
        db.RootFolders.Add(folder);
        await db.SaveChangesAsync(ct);
        return folder;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.RootFolders.FindAsync([id], ct);
        if (existing is not null)
        {
            db.RootFolders.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
