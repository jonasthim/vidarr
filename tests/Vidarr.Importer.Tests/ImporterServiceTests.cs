using Vidarr.Contracts.Models;
using Vidarr.Importer;
using Vidarr.Naming;
using Vidarr.Tests.Common;

namespace Vidarr.Importer.Tests;

public class ImporterServiceTests
{
    private static (ImporterService Sut, FakeFileSystem Fs) Build()
    {
        var fs = new FakeFileSystem();
        return (new ImporterService(fs, new NamingService()), fs);
    }

    private static ImportRequest BuildRequest(string source, FileOperation op = FileOperation.Move) => new(
        SourceFile: source,
        RootFolderPath: "/library",
        ArtistName: "Daft Punk",
        Title: "Around the World",
        Year: 1997,
        Quality: Quality.Webdl1080p,
        NamingConfig: NamingConfig.Default,
        FileOperation: op);

    [Fact]
    public async Task Move_relocates_source_to_templated_destination()
    {
        var (sut, fs) = Build();
        fs.WriteFakeText("/tmp/raw/video.mkv", "data");

        var result = await sut.ImportAsync(BuildRequest("/tmp/raw/video.mkv"), default);

        result.Success.Should().BeTrue();
        result.OperationPerformed.Should().Be(FileOperation.Move);
        result.DestinationPath.Should().EndWith("Daft Punk - Around the World (1997) [WEBDL-1080p].mkv");
        fs.FileExists("/tmp/raw/video.mkv").Should().BeFalse();
        fs.FileExists(result.DestinationPath!).Should().BeTrue();
    }

    [Fact]
    public async Task Copy_preserves_source()
    {
        var (sut, fs) = Build();
        fs.WriteFakeText("/tmp/raw/video.mkv", "data");

        var result = await sut.ImportAsync(BuildRequest("/tmp/raw/video.mkv", FileOperation.Copy), default);

        result.Success.Should().BeTrue();
        result.OperationPerformed.Should().Be(FileOperation.Copy);
        fs.FileExists("/tmp/raw/video.mkv").Should().BeTrue();
        fs.FileExists(result.DestinationPath!).Should().BeTrue();
    }

    [Fact]
    public async Task Hardlink_with_fallback_uses_hardlink_when_available()
    {
        var (sut, fs) = Build();
        fs.WriteFakeText("/tmp/raw/video.mkv", "data");
        var result = await sut.ImportAsync(BuildRequest("/tmp/raw/video.mkv", FileOperation.HardlinkWithFallback), default);

        result.Success.Should().BeTrue();
        result.OperationPerformed.Should().Be(FileOperation.HardlinkWithFallback);
        fs.FileExists("/tmp/raw/video.mkv").Should().BeTrue();
        fs.FileExists(result.DestinationPath!).Should().BeTrue();
    }

    [Fact]
    public async Task Missing_source_returns_failure()
    {
        var (sut, _) = Build();
        var result = await sut.ImportAsync(BuildRequest("/tmp/nonexistent.mkv"), default);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("not found");
    }

    [Fact]
    public async Task Destination_directory_is_created_when_missing()
    {
        var (sut, fs) = Build();
        fs.WriteFakeText("/tmp/raw/video.mkv", "data");
        var result = await sut.ImportAsync(BuildRequest("/tmp/raw/video.mkv"), default);
        result.Success.Should().BeTrue();
        fs.DirectoryExists(Path.GetDirectoryName(result.DestinationPath!)!).Should().BeTrue();
    }

    [Fact]
    public async Task Extra_source_label_is_passed_through_naming_tokens()
    {
        var (sut, fs) = Build();
        fs.WriteFakeText("/tmp/raw/video.mkv", "data");
        var request = BuildRequest("/tmp/raw/video.mkv") with
        {
            NamingConfig = NamingConfig.Default with { FileTemplate = "{Artist Name} - {Title} ({Year}) [{Source}]" },
            SourceLabel = "VEVO",
        };

        var result = await sut.ImportAsync(request, default);
        result.DestinationPath.Should().Contain("[VEVO]");
    }

    [Fact]
    public async Task IOException_during_move_is_caught_and_returned_as_failure()
    {
        var fs = new ThrowingFileSystem();
        var sut = new ImporterService(fs, new NamingService());
        var result = await sut.ImportAsync(BuildRequest("/tmp/raw/video.mkv"), default);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("disk full");
    }

    private sealed class ThrowingFileSystem : Vidarr.Contracts.Abstractions.IFileSystem
    {
        public bool FileExists(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public void DeleteFile(string path) { }
        public void MoveFile(string source, string destination, bool overwrite) => throw new IOException("disk full");
        public void CopyFile(string source, string destination, bool overwrite) { }
        public bool TryHardlink(string source, string destination) => false;
        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct) => Task.FromResult(Array.Empty<byte>());
        public Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken ct) => Task.CompletedTask;
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct) => Task.FromResult(string.Empty);
        public Task WriteAllTextAsync(string path, string contents, CancellationToken ct) => Task.CompletedTask;
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, bool recursive) => [];
        public IEnumerable<string> EnumerateDirectories(string path) => [];
        public long GetFileSize(string path) => 0;
        public Vidarr.Contracts.Abstractions.DiskInfo GetDiskInfo(string path) => new(0, 0, "x");
    }
}
