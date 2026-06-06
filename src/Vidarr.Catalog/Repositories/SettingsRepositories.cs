using Microsoft.EntityFrameworkCore;
using Vidarr.Catalog.Entities;

namespace Vidarr.Catalog.Repositories;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken ct);
    Task<Tag?> GetAsync(int id, CancellationToken ct);
    Task<Tag> AddAsync(Tag tag, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class TagRepository(VidarrDbContext db) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> ListAsync(CancellationToken ct) =>
        await db.Tags.OrderBy(t => t.Label).ToListAsync(ct);

    public Task<Tag?> GetAsync(int id, CancellationToken ct) =>
        db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Tag> AddAsync(Tag tag, CancellationToken ct)
    {
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return tag;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.Tags.FindAsync([id], ct);
        if (existing is not null)
        {
            db.Tags.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface IQualityProfileRepository
{
    Task<IReadOnlyList<QualityProfile>> ListAsync(CancellationToken ct);
    Task<QualityProfile?> GetAsync(int id, CancellationToken ct);
    Task<QualityProfile> AddAsync(QualityProfile profile, CancellationToken ct);
    Task UpdateAsync(QualityProfile profile, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class QualityProfileRepository(VidarrDbContext db) : IQualityProfileRepository
{
    public async Task<IReadOnlyList<QualityProfile>> ListAsync(CancellationToken ct) =>
        await db.QualityProfiles.OrderBy(p => p.Name).ToListAsync(ct);

    public Task<QualityProfile?> GetAsync(int id, CancellationToken ct) =>
        db.QualityProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<QualityProfile> AddAsync(QualityProfile profile, CancellationToken ct)
    {
        db.QualityProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public Task UpdateAsync(QualityProfile profile, CancellationToken ct)
    {
        db.QualityProfiles.Update(profile);
        return db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.QualityProfiles.FindAsync([id], ct);
        if (existing is not null)
        {
            db.QualityProfiles.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface ICustomFormatRepository
{
    Task<IReadOnlyList<CustomFormat>> ListAsync(CancellationToken ct);
    Task<CustomFormat?> GetAsync(int id, CancellationToken ct);
    Task<CustomFormat> AddAsync(CustomFormat format, CancellationToken ct);
    Task UpdateAsync(CustomFormat format, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class CustomFormatRepository(VidarrDbContext db) : ICustomFormatRepository
{
    public async Task<IReadOnlyList<CustomFormat>> ListAsync(CancellationToken ct) =>
        await db.CustomFormats.OrderBy(f => f.Name).ToListAsync(ct);

    public Task<CustomFormat?> GetAsync(int id, CancellationToken ct) =>
        db.CustomFormats.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<CustomFormat> AddAsync(CustomFormat format, CancellationToken ct)
    {
        db.CustomFormats.Add(format);
        await db.SaveChangesAsync(ct);
        return format;
    }

    public Task UpdateAsync(CustomFormat format, CancellationToken ct)
    {
        db.CustomFormats.Update(format);
        return db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.CustomFormats.FindAsync([id], ct);
        if (existing is not null)
        {
            db.CustomFormats.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface IBlocklistRepository
{
    Task<IReadOnlyList<BlocklistEntry>> ListAsync(CancellationToken ct);
    Task<BlocklistEntry?> GetAsync(int id, CancellationToken ct);
    Task<bool> ExistsForReleaseAsync(string releaseTitle, CancellationToken ct);
    Task<BlocklistEntry> AddAsync(BlocklistEntry entry, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class BlocklistRepository(VidarrDbContext db) : IBlocklistRepository
{
    public async Task<IReadOnlyList<BlocklistEntry>> ListAsync(CancellationToken ct) =>
        await db.Blocklist.OrderByDescending(b => b.Date).ToListAsync(ct);

    public Task<BlocklistEntry?> GetAsync(int id, CancellationToken ct) =>
        db.Blocklist.FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<bool> ExistsForReleaseAsync(string releaseTitle, CancellationToken ct) =>
        db.Blocklist.AnyAsync(b => b.ReleaseTitle == releaseTitle, ct);

    public async Task<BlocklistEntry> AddAsync(BlocklistEntry entry, CancellationToken ct)
    {
        db.Blocklist.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.Blocklist.FindAsync([id], ct);
        if (existing is not null)
        {
            db.Blocklist.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface IHistoryRepository
{
    Task<IReadOnlyList<HistoryEvent>> ListAsync(int? artistId, int? musicVideoId, int take, CancellationToken ct);
    Task<HistoryEvent> AddAsync(HistoryEvent evt, CancellationToken ct);
}

public sealed class HistoryRepository(VidarrDbContext db) : IHistoryRepository
{
    public async Task<IReadOnlyList<HistoryEvent>> ListAsync(int? artistId, int? musicVideoId, int take, CancellationToken ct)
    {
        IQueryable<HistoryEvent> q = db.History;
        if (artistId is { } aid) q = q.Where(h => h.ArtistId == aid);
        if (musicVideoId is { } mvid) q = q.Where(h => h.MusicVideoId == mvid);
        return await q.OrderByDescending(h => h.Date).Take(take).ToListAsync(ct);
    }

    public async Task<HistoryEvent> AddAsync(HistoryEvent evt, CancellationToken ct)
    {
        db.History.Add(evt);
        await db.SaveChangesAsync(ct);
        return evt;
    }
}

public interface IIndexerConfigRepository
{
    Task<IReadOnlyList<IndexerConfig>> ListAsync(CancellationToken ct);
    Task<IndexerConfig?> GetAsync(int id, CancellationToken ct);
    Task<IndexerConfig> AddAsync(IndexerConfig config, CancellationToken ct);
    Task UpdateAsync(IndexerConfig config, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class IndexerConfigRepository(VidarrDbContext db) : IIndexerConfigRepository
{
    public async Task<IReadOnlyList<IndexerConfig>> ListAsync(CancellationToken ct) =>
        await db.Indexers.OrderBy(i => i.Priority).ThenBy(i => i.Name).ToListAsync(ct);

    public Task<IndexerConfig?> GetAsync(int id, CancellationToken ct) =>
        db.Indexers.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IndexerConfig> AddAsync(IndexerConfig config, CancellationToken ct)
    {
        db.Indexers.Add(config);
        await db.SaveChangesAsync(ct);
        return config;
    }

    public Task UpdateAsync(IndexerConfig config, CancellationToken ct)
    {
        db.Indexers.Update(config);
        return db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.Indexers.FindAsync([id], ct);
        if (existing is not null)
        {
            db.Indexers.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface IDownloadClientConfigRepository
{
    Task<IReadOnlyList<DownloadClientConfig>> ListAsync(CancellationToken ct);
    Task<DownloadClientConfig?> GetAsync(int id, CancellationToken ct);
    Task<DownloadClientConfig> AddAsync(DownloadClientConfig config, CancellationToken ct);
    Task UpdateAsync(DownloadClientConfig config, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class DownloadClientConfigRepository(VidarrDbContext db) : IDownloadClientConfigRepository
{
    public async Task<IReadOnlyList<DownloadClientConfig>> ListAsync(CancellationToken ct) =>
        await db.DownloadClients.OrderBy(c => c.Priority).ThenBy(c => c.Name).ToListAsync(ct);

    public Task<DownloadClientConfig?> GetAsync(int id, CancellationToken ct) =>
        db.DownloadClients.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<DownloadClientConfig> AddAsync(DownloadClientConfig config, CancellationToken ct)
    {
        db.DownloadClients.Add(config);
        await db.SaveChangesAsync(ct);
        return config;
    }

    public Task UpdateAsync(DownloadClientConfig config, CancellationToken ct)
    {
        db.DownloadClients.Update(config);
        return db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.DownloadClients.FindAsync([id], ct);
        if (existing is not null)
        {
            db.DownloadClients.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface INotificationConfigRepository
{
    Task<IReadOnlyList<NotificationConfig>> ListAsync(CancellationToken ct);
    Task<NotificationConfig?> GetAsync(int id, CancellationToken ct);
    Task<NotificationConfig> AddAsync(NotificationConfig config, CancellationToken ct);
    Task UpdateAsync(NotificationConfig config, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class NotificationConfigRepository(VidarrDbContext db) : INotificationConfigRepository
{
    public async Task<IReadOnlyList<NotificationConfig>> ListAsync(CancellationToken ct) =>
        await db.Notifications.OrderBy(n => n.Name).ToListAsync(ct);

    public Task<NotificationConfig?> GetAsync(int id, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<NotificationConfig> AddAsync(NotificationConfig config, CancellationToken ct)
    {
        db.Notifications.Add(config);
        await db.SaveChangesAsync(ct);
        return config;
    }

    public Task UpdateAsync(NotificationConfig config, CancellationToken ct)
    {
        db.Notifications.Update(config);
        return db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.Notifications.FindAsync([id], ct);
        if (existing is not null)
        {
            db.Notifications.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface IDiscoveryRuleSetRepository
{
    Task<IReadOnlyList<DiscoveryRuleSet>> ListAsync(CancellationToken ct);
    Task<DiscoveryRuleSet?> GetAsync(int id, CancellationToken ct);
    Task<DiscoveryRuleSet> AddAsync(DiscoveryRuleSet rule, CancellationToken ct);
    Task UpdateAsync(DiscoveryRuleSet rule, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class DiscoveryRuleSetRepository(VidarrDbContext db) : IDiscoveryRuleSetRepository
{
    public async Task<IReadOnlyList<DiscoveryRuleSet>> ListAsync(CancellationToken ct) =>
        await db.DiscoveryRuleSets.OrderBy(r => r.Name).ToListAsync(ct);

    public Task<DiscoveryRuleSet?> GetAsync(int id, CancellationToken ct) =>
        db.DiscoveryRuleSets.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<DiscoveryRuleSet> AddAsync(DiscoveryRuleSet rule, CancellationToken ct)
    {
        db.DiscoveryRuleSets.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public Task UpdateAsync(DiscoveryRuleSet rule, CancellationToken ct)
    {
        db.DiscoveryRuleSets.Update(rule);
        return db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await db.DiscoveryRuleSets.FindAsync([id], ct);
        if (existing is not null)
        {
            db.DiscoveryRuleSets.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}

public interface IApplicationConfigRepository
{
    /// <summary>Always returns a non-null ApplicationConfig — seed creates one on startup.</summary>
    Task<ApplicationConfig> GetAsync(CancellationToken ct);
    Task UpdateAsync(ApplicationConfig config, CancellationToken ct);
}

public sealed class ApplicationConfigRepository(VidarrDbContext db) : IApplicationConfigRepository
{
    public async Task<ApplicationConfig> GetAsync(CancellationToken ct)
    {
        var existing = await db.ApplicationConfigs.FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        var fresh = new ApplicationConfig { Updated = DateTimeOffset.UtcNow };
        db.ApplicationConfigs.Add(fresh);
        await db.SaveChangesAsync(ct);
        return fresh;
    }

    public Task UpdateAsync(ApplicationConfig config, CancellationToken ct)
    {
        config.Updated = DateTimeOffset.UtcNow;
        db.ApplicationConfigs.Update(config);
        return db.SaveChangesAsync(ct);
    }
}
