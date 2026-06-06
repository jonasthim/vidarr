using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;

namespace Vidarr.Catalog.Tests;

public class MusicVideoFileAndRootFolderTests
{
    [Fact]
    public async Task MusicVideoFile_round_trip_and_lookup_by_video_id()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var artistRepo = new ArtistRepository(db);
            var videoRepo = new MusicVideoRepository(db);
            var fileRepo = new MusicVideoFileRepository(db);
            var artist = await artistRepo.AddAsync(new Artist { Name = "A", SortName = "A" }, CancellationToken.None);
            var video = await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "T" }, CancellationToken.None);

            var file = await fileRepo.AddAsync(new MusicVideoFile
            {
                MusicVideoId = video.Id,
                RelativePath = "A/T.mkv",
                SizeBytes = 12345,
                DateAdded = DateTimeOffset.UtcNow,
                QualityId = 4,
            }, CancellationToken.None);

            var byId = await fileRepo.GetAsync(file.Id, CancellationToken.None);
            var byVideo = await fileRepo.GetByMusicVideoIdAsync(video.Id, CancellationToken.None);
            byId.Should().NotBeNull();
            byVideo.Should().NotBeNull();
            byVideo!.RelativePath.Should().Be("A/T.mkv");
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task MusicVideoFile_delete_removes_row()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var artistRepo = new ArtistRepository(db);
            var videoRepo = new MusicVideoRepository(db);
            var fileRepo = new MusicVideoFileRepository(db);
            var artist = await artistRepo.AddAsync(new Artist { Name = "A", SortName = "A" }, CancellationToken.None);
            var video = await videoRepo.AddAsync(new MusicVideo { ArtistId = artist.Id, Title = "T" }, CancellationToken.None);
            var file = await fileRepo.AddAsync(new MusicVideoFile
            {
                MusicVideoId = video.Id,
                RelativePath = "A/T.mkv",
                DateAdded = DateTimeOffset.UtcNow,
                QualityId = 4,
            }, CancellationToken.None);

            await fileRepo.DeleteAsync(file.Id, CancellationToken.None);
            (await fileRepo.GetAsync(file.Id, CancellationToken.None)).Should().BeNull();

            // Deleting unknown id is a no-op
            await fileRepo.DeleteAsync(99999, CancellationToken.None);
        }
        finally
        {
            conn.Dispose();
        }
    }

    [Fact]
    public async Task RootFolder_CRUD()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new RootFolderRepository(db);
            var folder = await sut.AddAsync(new RootFolder { Path = "/library", Accessible = true, FreeBytes = 100, TotalBytes = 200 }, CancellationToken.None);
            await sut.AddAsync(new RootFolder { Path = "/library2", Accessible = false }, CancellationToken.None);

            var list = await sut.ListAsync(CancellationToken.None);
            list.Should().HaveCount(2);

            var fetched = await sut.GetAsync(folder.Id, CancellationToken.None);
            fetched!.Path.Should().Be("/library");

            await sut.DeleteAsync(folder.Id, CancellationToken.None);
            (await sut.GetAsync(folder.Id, CancellationToken.None)).Should().BeNull();

            await sut.DeleteAsync(99999, CancellationToken.None);
        }
        finally
        {
            conn.Dispose();
        }
    }
}
