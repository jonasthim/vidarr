using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Catalog.Tests;

public class SettingsRepositoryTests
{
    [Fact]
    public async Task Tag_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new TagRepository(db);
            var a = await sut.AddAsync(new Tag { Label = "favorites" }, default);
            var b = await sut.AddAsync(new Tag { Label = "metal" }, default);
            (await sut.ListAsync(default)).Select(t => t.Label).Should().Equal("favorites", "metal");
            (await sut.GetAsync(a.Id, default))!.Label.Should().Be("favorites");

            await sut.DeleteAsync(a.Id, default);
            (await sut.GetAsync(a.Id, default)).Should().BeNull();
            await sut.DeleteAsync(9999, default); // no-op
            _ = b;
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task QualityProfile_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new QualityProfileRepository(db);
            var p = await sut.AddAsync(new QualityProfile
            {
                Name = "HD",
                AllowedQualityIdsJson = "[3,4]",
                CutoffQualityId = 4,
                UpgradeAllowed = true,
            }, default);

            p.UpgradeAllowed = false;
            await sut.UpdateAsync(p, default);
            (await sut.GetAsync(p.Id, default))!.UpgradeAllowed.Should().BeFalse();

            (await sut.ListAsync(default)).Should().ContainSingle();

            await sut.DeleteAsync(p.Id, default);
            (await sut.GetAsync(p.Id, default)).Should().BeNull();
            await sut.DeleteAsync(9999, default);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task CustomFormat_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new CustomFormatRepository(db);
            var f = await sut.AddAsync(new CustomFormat { Name = "VEVO", SpecificationsJson = "[]" }, default);
            f.IncludeCustomFormatWhenRenaming = true;
            await sut.UpdateAsync(f, default);
            (await sut.GetAsync(f.Id, default))!.IncludeCustomFormatWhenRenaming.Should().BeTrue();
            (await sut.ListAsync(default)).Should().ContainSingle();
            await sut.DeleteAsync(f.Id, default);
            await sut.DeleteAsync(9999, default);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task Blocklist_CRUD_and_release_lookup()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new BlocklistRepository(db);
            var older = await sut.AddAsync(new BlocklistEntry
            {
                ReleaseTitle = "Old Release",
                IndexerName = "X",
                Date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }, default);
            var newer = await sut.AddAsync(new BlocklistEntry
            {
                ReleaseTitle = "New Release",
                IndexerName = "X",
                Date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            }, default);

            var list = await sut.ListAsync(default);
            list.Should().HaveCount(2);
            list[0].Id.Should().Be(newer.Id); // ordered by date desc

            (await sut.ExistsForReleaseAsync("New Release", default)).Should().BeTrue();
            (await sut.ExistsForReleaseAsync("Unknown", default)).Should().BeFalse();

            (await sut.GetAsync(older.Id, default)).Should().NotBeNull();

            await sut.DeleteAsync(newer.Id, default);
            (await sut.GetAsync(newer.Id, default)).Should().BeNull();
            await sut.DeleteAsync(9999, default);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task History_filter_by_artist_and_video_and_take()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new HistoryRepository(db);
            await sut.AddAsync(new HistoryEvent { EventType = HistoryEventType.Grabbed, Date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), ArtistId = 1, MusicVideoId = 10 }, default);
            await sut.AddAsync(new HistoryEvent { EventType = HistoryEventType.Imported, Date = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), ArtistId = 1, MusicVideoId = 10 }, default);
            await sut.AddAsync(new HistoryEvent { EventType = HistoryEventType.Grabbed, Date = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), ArtistId = 2, MusicVideoId = 20 }, default);

            (await sut.ListAsync(null, null, 10, default)).Should().HaveCount(3);
            (await sut.ListAsync(1, null, 10, default)).Should().HaveCount(2);
            (await sut.ListAsync(null, 20, 10, default)).Should().HaveCount(1);
            (await sut.ListAsync(null, null, 2, default)).Should().HaveCount(2); // newest first
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task IndexerConfig_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new IndexerConfigRepository(db);
            var a = await sut.AddAsync(new IndexerConfig { Name = "YT", Implementation = "YouTube", Priority = 1 }, default);
            await sut.AddAsync(new IndexerConfig { Name = "NZB", Implementation = "Newznab", Priority = 5 }, default);

            (await sut.ListAsync(default)).Select(i => i.Name).Should().Equal("YT", "NZB"); // priority asc

            a.Priority = 99;
            await sut.UpdateAsync(a, default);
            (await sut.GetAsync(a.Id, default))!.Priority.Should().Be(99);
            await sut.DeleteAsync(a.Id, default);
            await sut.DeleteAsync(9999, default);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task DownloadClientConfig_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new DownloadClientConfigRepository(db);
            var a = await sut.AddAsync(new DownloadClientConfig { Name = "qBit", Implementation = "QBittorrent" }, default);
            await sut.AddAsync(new DownloadClientConfig { Name = "yt-dlp", Implementation = "YtDlp" }, default);
            (await sut.ListAsync(default)).Should().HaveCount(2);
            a.Enable = false;
            await sut.UpdateAsync(a, default);
            (await sut.GetAsync(a.Id, default))!.Enable.Should().BeFalse();
            await sut.DeleteAsync(a.Id, default);
            await sut.DeleteAsync(9999, default);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task NotificationConfig_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new NotificationConfigRepository(db);
            var a = await sut.AddAsync(new NotificationConfig { Name = "WH", Implementation = "Webhook" }, default);
            (await sut.ListAsync(default)).Should().ContainSingle();
            a.Enable = false;
            await sut.UpdateAsync(a, default);
            (await sut.GetAsync(a.Id, default))!.Enable.Should().BeFalse();
            await sut.DeleteAsync(a.Id, default);
            await sut.DeleteAsync(9999, default);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task DiscoveryRuleSet_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new DiscoveryRuleSetRepository(db);
            var r = await sut.AddAsync(new DiscoveryRuleSet { Name = "Synth 2020s" }, default);
            r.Enabled = false;
            await sut.UpdateAsync(r, default);
            (await sut.GetAsync(r.Id, default))!.Enabled.Should().BeFalse();
            (await sut.ListAsync(default)).Should().ContainSingle();
            await sut.DeleteAsync(r.Id, default);
            await sut.DeleteAsync(9999, default);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task ApplicationConfig_get_returns_default_and_round_trips_update()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new ApplicationConfigRepository(db);
            var first = await sut.GetAsync(default);
            first.Should().NotBeNull();
            first.InstanceName.Should().Be("Vidarr");

            first.InstanceName = "Custom";
            await sut.UpdateAsync(first, default);

            var second = await sut.GetAsync(default);
            second.InstanceName.Should().Be("Custom");
        }
        finally { conn.Dispose(); }
    }
}
