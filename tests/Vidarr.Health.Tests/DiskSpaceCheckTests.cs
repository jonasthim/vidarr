using Vidarr.Catalog.Entities;
using Vidarr.Contracts.Events;
using Vidarr.Tests.Common;

namespace Vidarr.Health.Tests;

public class DiskSpaceCheckTests
{
    [Fact]
    public async Task Returns_no_issues_when_free_space_is_healthy()
    {
        var repo = new FakeRootFolderRepository();
        await repo.AddAsync(new RootFolder { Path = "/data/music" }, default);
        var fs = new FakeFileSystem();
        fs.CreateDirectory("/data/music");
        fs.SetDisk(totalBytes: 1_000_000_000, freeBytes: 500_000_000); // 50% free

        var check = new DiskSpaceCheck(repo, fs);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Raises_warning_below_warn_fraction()
    {
        var repo = new FakeRootFolderRepository();
        await repo.AddAsync(new RootFolder { Path = "/data/music" }, default);
        var fs = new FakeFileSystem();
        fs.CreateDirectory("/data/music");
        fs.SetDisk(1_000_000_000, 80_000_000); // 8% free → warn (10% threshold)

        var check = new DiskSpaceCheck(repo, fs);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Warning);
    }

    [Fact]
    public async Task Raises_error_below_error_fraction()
    {
        var repo = new FakeRootFolderRepository();
        await repo.AddAsync(new RootFolder { Path = "/data/music" }, default);
        var fs = new FakeFileSystem();
        fs.CreateDirectory("/data/music");
        fs.SetDisk(1_000_000_000, 20_000_000); // 2% free → error (5% threshold)

        var check = new DiskSpaceCheck(repo, fs);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Error);
    }

    [Fact]
    public async Task Skips_inaccessible_folders()
    {
        var repo = new FakeRootFolderRepository();
        await repo.AddAsync(new RootFolder { Path = "/missing" }, default);
        var fs = new FakeFileSystem();
        // Note: /missing not created → DirectoryExists==false

        var check = new DiskSpaceCheck(repo, fs);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty(); // RootFolderAccessibleCheck owns this surface
    }

    [Fact]
    public async Task Returns_no_issues_when_no_folders_configured()
    {
        var repo = new FakeRootFolderRepository();
        var fs = new FakeFileSystem();

        var check = new DiskSpaceCheck(repo, fs);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty();
    }

    [Fact]
    public void Name_returns_class_name()
    {
        var check = new DiskSpaceCheck(new FakeRootFolderRepository(), new FakeFileSystem());
        check.Name.Should().Be("DiskSpaceCheck");
    }
}
