using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;
using Vidarr.Notifications;
using Vidarr.Tests.Common;

namespace Vidarr.Notifications.Tests;

public class JellyfinNotifierTests
{
    private static readonly Uri SampleBase = new("http://localhost:8096/");

    private static JellyfinNotifier Build(FakeHttpClient http) =>
        new(1, "Jellyfin",
            new JellyfinSettings(SampleBase, "api-key-123"),
            new HashSet<NotificationEventType>
            {
                NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
                NotificationEventType.OnDelete, NotificationEventType.OnTest,
            },
            http);

    [Fact]
    public async Task OnImport_posts_library_refresh_with_emby_token_header()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnImportAsync(new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/x.mkv", 100, Quality.Webdl720p, null), default);

        var req = http.Requests.Single();
        req.Method.Should().Be(HttpMethod.Post);
        req.Uri.AbsolutePath.Should().Be("/Library/Refresh");
        req.Headers!.Should().ContainKey("X-Emby-Token").WhoseValue.Should().Be("api-key-123");
    }

    [Fact]
    public async Task OnImport_throws_on_non_2xx()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(403, new Dictionary<string, string>(), ""));
        await FluentActions.Invoking(() => Build(http).OnImportAsync(
            new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/x", 0, Quality.Webdl720p, null), default))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*403*");
    }

    [Fact]
    public async Task OnGrab_is_no_op()
    {
        var http = new FakeHttpClient();
        await Build(http).OnGrabAsync(new GrabEvent(DateTimeOffset.UtcNow, 1, [2], "R", "I", "DC", Quality.Webdl720p), default);
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_hits_system_info_public()
    {
        var http = new FakeHttpClient().WhenRequest(
            r => r.Uri.AbsolutePath == "/System/Info/Public",
            new HttpClientResponse(200, new Dictionary<string, string>(), "{}"));
        var result = await Build(http).OnTestAsync(default);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Test_returns_failure_on_404()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(404, new Dictionary<string, string>(), ""));
        var result = await Build(http).OnTestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("404");
    }
}
