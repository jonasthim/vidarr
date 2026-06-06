using Vidarr.Catalog.Entities;
using Vidarr.Contracts.Events;
using Vidarr.Tests.Common;

namespace Vidarr.Health.Tests;

public class RootFolderAccessibleCheckTests
{
    [Fact]
    public async Task Returns_no_issues_when_all_folders_exist()
    {
        var repo = new FakeRootFolderRepository();
        await repo.AddAsync(new RootFolder { Path = "/data/music" }, default);
        var fs = new FakeFileSystem();
        fs.CreateDirectory("/data/music");

        var check = new RootFolderAccessibleCheck(repo, fs);
        var issues = await check.RunAsync(default);
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Raises_error_for_missing_folder()
    {
        var repo = new FakeRootFolderRepository();
        await repo.AddAsync(new RootFolder { Path = "/missing" }, default);
        var fs = new FakeFileSystem();

        var check = new RootFolderAccessibleCheck(repo, fs);
        var issues = await check.RunAsync(default);
        issues.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Error);
    }

    [Fact]
    public void Name_returns_class_name()
    {
        var check = new RootFolderAccessibleCheck(new FakeRootFolderRepository(), new FakeFileSystem());
        check.Name.Should().Be("RootFolderAccessibleCheck");
    }
}
