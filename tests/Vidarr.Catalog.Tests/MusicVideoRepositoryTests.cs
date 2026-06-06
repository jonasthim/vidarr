using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Catalog.Tests;

public class MusicVideoRepositoryTests
{
    [Fact]
    public async Task List_by_artist_orders_by_year_then_title()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var artistRepo = new ArtistRepository(db);
            var videoRepo = new MusicVideoRepository(db);
            var artist = await artistRepo.AddAsync(new Artist { Name = "A", SortName = "A" }, CancellationToken.None);

            await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "Zenith", Year = 2000 }, CancellationToken.None);
            await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "Alpha", Year = 1999 }, CancellationToken.None);
            await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "Beta", Year = 1999 }, CancellationToken.None);

            var list = await videoRepo.ListByArtistAsync(artist.Id, CancellationToken.None);
            list.Select(v => v.Title).Should().Equal("Alpha", "Beta", "Zenith");
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task List_wanted_returns_monitored_without_files()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var artistRepo = new ArtistRepository(db);
            var videoRepo = new MusicVideoRepository(db);
            var artist = await artistRepo.AddAsync(new Artist { Name = "A", SortName = "A" }, CancellationToken.None);

            await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "Wanted1", Monitored = true, HasFile = false }, CancellationToken.None);
            await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "Wanted2", Monitored = true, HasFile = false }, CancellationToken.None);
            await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "HasFile", Monitored = true, HasFile = true }, CancellationToken.None);
            await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "NotMonitored", Monitored = false, HasFile = false }, CancellationToken.None);

            var wanted = await videoRepo.ListWantedAsync(CancellationToken.None);
            wanted.Should().HaveCount(2);
            wanted.Select(v => v.Title).Should().BeEquivalentTo(["Wanted1", "Wanted2"]);
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task Update_persists_changes()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var artistRepo = new ArtistRepository(db);
            var videoRepo = new MusicVideoRepository(db);
            var artist = await artistRepo.AddAsync(new Artist { Name = "A", SortName = "A" }, CancellationToken.None);
            var video = await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "T" }, CancellationToken.None);

            video.Monitored = true;
            await videoRepo.UpdateAsync(video, CancellationToken.None);

            var fetched = await videoRepo.GetAsync(video.Id, CancellationToken.None);
            fetched!.Monitored.Should().BeTrue();
        }
        finally
        {
            conn.Dispose();
        }
    }
}
