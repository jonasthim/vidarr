using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;
using Vidarr.Notifications;
using Vidarr.Tests.Common;

namespace Vidarr.Notifications.Tests;

public class WebhookNotifierTests
{
    private static readonly Uri Endpoint = new("https://hook.example.com/vidarr");

    private static (WebhookNotifier Sut, FakeHttpClient Http) Build(int responseStatus = 200, IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(responseStatus, new Dictionary<string, string>(), ""));
        var settings = new WebhookSettings(Endpoint, extraHeaders ?? new Dictionary<string, string>());
        var supported = new HashSet<NotificationEventType>
        {
            NotificationEventType.OnGrab, NotificationEventType.OnImport,
            NotificationEventType.OnUpgrade, NotificationEventType.OnDelete,
            NotificationEventType.OnHealthIssue, NotificationEventType.OnTest,
        };
        return (new WebhookNotifier(1, "Webhook", settings, supported, http), http);
    }

    [Fact]
    public async Task OnImport_posts_JSON_payload_with_event_type_envelope()
    {
        var (sut, http) = Build();
        var evt = new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/library/A/x.mkv", 123, Quality.Webdl1080p, "VEVO");
        await sut.OnImportAsync(evt, default);

        var req = http.Requests.Should().ContainSingle().Which;
        req.Method.Should().Be(HttpMethod.Post);
        req.Uri.Should().Be(Endpoint);
        req.Content.Should().BeOfType<HttpClientContent.Json>();
        var body = ((HttpClientContent.Json)req.Content!).Body;
        body.Should().Contain("\"eventType\":\"import\"");
        body.Should().Contain("/library/A/x.mkv");
    }

    [Fact]
    public async Task OnGrab_uses_grab_event_type()
    {
        var (sut, http) = Build();
        await sut.OnGrabAsync(new GrabEvent(DateTimeOffset.UtcNow, 1, [2], "R", "I", "DC", Quality.Webdl720p), default);
        ((HttpClientContent.Json)http.Requests.Single().Content!).Body.Should().Contain("\"eventType\":\"grab\"");
    }

    [Fact]
    public async Task OnUpgrade_uses_upgrade_event_type()
    {
        var (sut, http) = Build();
        await sut.OnUpgradeAsync(new UpgradeEvent(DateTimeOffset.UtcNow, 1, 2, "p", Quality.Webdl480p, Quality.Webdl1080p), default);
        ((HttpClientContent.Json)http.Requests.Single().Content!).Body.Should().Contain("\"eventType\":\"upgrade\"");
    }

    [Fact]
    public async Task OnDelete_uses_delete_event_type()
    {
        var (sut, http) = Build();
        await sut.OnDeleteAsync(new DeleteEvent(DateTimeOffset.UtcNow, 1, 2, "/p"), default);
        ((HttpClientContent.Json)http.Requests.Single().Content!).Body.Should().Contain("\"eventType\":\"delete\"");
    }

    [Fact]
    public async Task OnHealthIssue_uses_health_issue_event_type()
    {
        var (sut, http) = Build();
        await sut.OnHealthIssueAsync(new HealthIssueEvent(DateTimeOffset.UtcNow, "DiskSpace", HealthSeverity.Warning, "low", false), default);
        ((HttpClientContent.Json)http.Requests.Single().Content!).Body.Should().Contain("\"eventType\":\"healthIssue\"");
    }

    [Fact]
    public async Task OnTest_returns_success_for_2xx()
    {
        var (sut, _) = Build(responseStatus: 204);
        var result = await sut.OnTestAsync(default);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("204");
    }

    [Fact]
    public async Task OnTest_returns_failure_for_5xx_with_truncated_body()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(500, new Dictionary<string, string>(), new string('x', 500)));
        var settings = new WebhookSettings(Endpoint, new Dictionary<string, string>());
        var sut = new WebhookNotifier(1, "W", settings, new HashSet<NotificationEventType> { NotificationEventType.OnTest }, http);

        var result = await sut.OnTestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("500");
        result.Message.Should().Contain("...");
        result.Message!.Length.Should().BeLessThan(250);
    }

    [Fact]
    public async Task Headers_from_settings_are_added_to_each_request()
    {
        var (sut, http) = Build(extraHeaders: new Dictionary<string, string> { ["X-Signature"] = "abc" });
        await sut.OnImportAsync(new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "p", 1, Quality.Webdl720p, null), default);
        http.Requests.Single().Headers!.Should().ContainKey("X-Signature").WhoseValue.Should().Be("abc");
    }

    [Fact]
    public void Supported_events_are_advertised()
    {
        var (sut, _) = Build();
        sut.SupportedEvents.Should().Contain(NotificationEventType.OnImport);
        sut.Id.Should().Be(1);
        sut.Name.Should().Be("Webhook");
    }
}
