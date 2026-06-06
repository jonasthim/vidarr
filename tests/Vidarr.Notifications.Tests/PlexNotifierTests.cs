using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;
using Vidarr.Notifications;
using Vidarr.Tests.Common;

namespace Vidarr.Notifications.Tests;

public class PlexNotifierTests
{
    private static readonly Uri SampleBase = new("http://localhost:32400/");

    private static PlexNotifier Build(FakeHttpClient http, int? sectionId = 3) =>
        new(1, "Plex",
            new PlexSettings(SampleBase, "test-token", sectionId),
            new HashSet<NotificationEventType>
            {
                NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
                NotificationEventType.OnDelete, NotificationEventType.OnTest,
            },
            http);

    [Fact]
    public async Task OnImport_puts_to_library_section_refresh_with_token_header()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(), ""));
        await Build(http).OnImportAsync(new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/x.mkv", 100, Quality.Webdl720p, null), default);

        var req = http.Requests.Single();
        req.Method.Should().Be(HttpMethod.Put);
        req.Uri.AbsolutePath.Should().Be("/library/sections/3/refresh");
        req.Headers!.Should().ContainKey("X-Plex-Token").WhoseValue.Should().Be("test-token");
    }

    [Fact]
    public async Task OnImport_no_op_when_section_id_not_configured()
    {
        var http = new FakeHttpClient();
        await Build(http, sectionId: null).OnImportAsync(new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/x", 0, Quality.Webdl720p, null), default);
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task OnImport_throws_on_non_2xx()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), ""));
        await FluentActions.Invoking(() => Build(http).OnImportAsync(
            new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/x", 0, Quality.Webdl720p, null), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*500*");
    }

    [Fact]
    public async Task OnGrab_is_no_op()
    {
        var http = new FakeHttpClient();
        await Build(http).OnGrabAsync(new GrabEvent(DateTimeOffset.UtcNow, 1, [2], "R", "I", "DC", Quality.Webdl720p), default);
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task OnUpgrade_triggers_refresh()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(), ""));
        await Build(http).OnUpgradeAsync(new UpgradeEvent(DateTimeOffset.UtcNow, 1, 2, "/p", Quality.Webdl720p, Quality.Webdl1080p), default);
        http.Requests.Single().Uri.AbsolutePath.Should().Be("/library/sections/3/refresh");
    }

    [Fact]
    public async Task OnDelete_triggers_refresh()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(200, new Dictionary<string, string>(), ""));
        await Build(http).OnDeleteAsync(new DeleteEvent(DateTimeOffset.UtcNow, 1, 2, "/p"), default);
        http.Requests.Single().Uri.AbsolutePath.Should().Be("/library/sections/3/refresh");
    }

    [Fact]
    public async Task Test_returns_success_when_sections_returns_2xx()
    {
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath == "/library/sections",
            new HttpClientResponse(200, new Dictionary<string, string>(), "{}"));
        var result = await Build(http).OnTestAsync(default);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Test_returns_failure_on_401()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(401, new Dictionary<string, string>(), ""));
        var result = await Build(http).OnTestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("401");
    }
}
