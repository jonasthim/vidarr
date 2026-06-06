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
        await db.MusicVideos.Where(v => v.Monitored && !v.HasFile).ToListAsync(ct);

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
