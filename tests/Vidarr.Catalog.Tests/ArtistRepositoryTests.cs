using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Models;

namespace Vidarr.Catalog.Tests;

public class ArtistRepositoryTests
{
    [Fact]
    public async Task Add_then_get_round_trips()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new ArtistRepository(db);
            var artist = new Artist
            {
                Name = "Daft Punk",
                SortName = "Daft Punk",
                ExternalIdsJson = """{"imvdb":"123"}""",
                RootFolderPath = "/library",
                Added = DateTimeOffset.UtcNow,
                MonitorMode = MonitorMode.All,
            };

            await sut.AddAsync(artist, CancellationToken.None);
            var fetched = await sut.GetAsync(artist.Id, CancellationToken.None);

            fetched.Should().NotBeNull();
            fetched!.Name.Should().Be("Daft Punk");
            fetched.MonitorMode.Should().Be(MonitorMode.All);
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task Find_by_external_id_returns_matching_artist()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new ArtistRepository(db);
            await sut.AddAsync(new Artist
            {
                Name = "Daft Punk",
                SortName = "Daft Punk",
                ExternalIdsJson = """{"imvdb":"123","musicbrainz":"abc"}""",
            }, CancellationToken.None);

            var found = await sut.FindByExternalIdAsync("imvdb", "123", CancellationToken.None);
            found.Should().NotBeNull();
            found!.Name.Should().Be("Daft Punk");
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task List_orders_by_sort_name()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new ArtistRepository(db);
            await sut.AddAsync(new Artist { Name = "Madonna", SortName = "Madonna" }, CancellationToken.None);
            await sut.AddAsync(new Artist { Name = "Daft Punk", SortName = "Daft Punk" }, CancellationToken.None);

            var list = await sut.ListAsync(CancellationToken.None);
            list.Select(a => a.SortName).Should().Equal("Daft Punk", "Madonna");
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task Delete_removes_artist()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new ArtistRepository(db);
            var artist = await sut.AddAsync(new Artist { Name = "A", SortName = "A" }, CancellationToken.None);

            await sut.DeleteAsync(artist.Id, CancellationToken.None);

            var fetched = await sut.GetAsync(artist.Id, CancellationToken.None);
            fetched.Should().BeNull();
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
            var sut = new ArtistRepository(db);
            var artist = await sut.AddAsync(new Artist { Name = "A", SortName = "A" }, CancellationToken.None);

            artist.Name = "AAA";
            await sut.UpdateAsync(artist, CancellationToken.None);

            var fetched = await sut.GetAsync(artist.Id, CancellationToken.None);
            fetched!.Name.Should().Be("AAA");
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task Delete_unknown_id_is_no_op()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new ArtistRepository(db);
            await sut.DeleteAsync(9999, CancellationToken.None);
        }
        finally
        {
            conn.Dispose();
        }
    }
}
