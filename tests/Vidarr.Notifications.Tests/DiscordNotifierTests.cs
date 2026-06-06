using System.Text.Json;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;
using Vidarr.Notifications;
using Vidarr.Tests.Common;

namespace Vidarr.Notifications.Tests;

public class DiscordNotifierTests
{
    private static readonly Uri Webhook = new("https://discord.com/api/webhooks/test");

    private static DiscordNotifier Build(FakeHttpClient http) =>
        new(1, "Discord",
            new DiscordSettings(Webhook, Username: "Vidarr", AvatarUrl: null),
            new HashSet<NotificationEventType>
            {
                NotificationEventType.OnGrab, NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
                NotificationEventType.OnDelete, NotificationEventType.OnHealthIssue, NotificationEventType.OnTest,
            },
            http);

    private static JsonElement EmbedFromRequest(HttpClientRequest req)
    {
        var body = ((HttpClientContent.Json)req.Content!).Body;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("embeds")[0].Clone();
    }

    [Fact]
    public async Task OnGrab_posts_embed_with_blue_color_and_three_fields()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnGrabAsync(
            new GrabEvent(DateTimeOffset.UtcNow, 1, [2], "Daft Punk - Around the World", "Newznab", "qBit", Quality.Webdl1080p), default);

        var req = http.Requests.Single();
        req.Uri.Should().Be(Webhook);
        var embed = EmbedFromRequest(req);
        embed.GetProperty("title").GetString().Should().Contain("Grabbed");
        embed.GetProperty("color").GetInt32().Should().Be(0x3498DB);
        embed.GetProperty("fields").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task OnImport_posts_embed_with_green_color()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnImportAsync(new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/x.mkv", 5_000_000_000, Quality.Webdl1080p, "VEVO"), default);
        var embed = EmbedFromRequest(http.Requests.Single());
        embed.GetProperty("color").GetInt32().Should().Be(0x2ECC71);
    }

    [Fact]
    public async Task OnUpgrade_posts_embed_with_yellow_color()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnUpgradeAsync(new UpgradeEvent(DateTimeOffset.UtcNow, 1, 2, "/p", Quality.Webdl720p, Quality.Webdl1080p), default);
        EmbedFromRequest(http.Requests.Single()).GetProperty("color").GetInt32().Should().Be(0xF1C40F);
    }

    [Fact]
    public async Task OnDelete_posts_embed_with_grey_color()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnDeleteAsync(new DeleteEvent(DateTimeOffset.UtcNow, 1, 2, "/p"), default);
        EmbedFromRequest(http.Requests.Single()).GetProperty("color").GetInt32().Should().Be(0x95A5A6);
    }

    [Fact]
    public async Task OnHealthIssue_severity_error_renders_red()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnHealthIssueAsync(new HealthIssueEvent(DateTimeOffset.UtcNow, "X", HealthSeverity.Error, "broken", false), default);
        EmbedFromRequest(http.Requests.Single()).GetProperty("color").GetInt32().Should().Be(0xE74C3C);
    }

    [Fact]
    public async Task OnHealthIssue_severity_warning_renders_yellow()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnHealthIssueAsync(new HealthIssueEvent(DateTimeOffset.UtcNow, "X", HealthSeverity.Warning, "warn", true), default);
        EmbedFromRequest(http.Requests.Single()).GetProperty("color").GetInt32().Should().Be(0xF1C40F);
    }

    [Fact]
    public async Task Username_override_is_forwarded_to_webhook_payload()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        await Build(http).OnGrabAsync(new GrabEvent(DateTimeOffset.UtcNow, 1, [2], "R", "I", "DC", Quality.Webdl720p), default);
        var body = ((HttpClientContent.Json)http.Requests.Single().Content!).Body;
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("username").GetString().Should().Be("Vidarr");
    }

    [Fact]
    public async Task Test_posts_a_probe_embed()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(204, new Dictionary<string, string>(), ""));
        var result = await Build(http).OnTestAsync(default);
        result.Success.Should().BeTrue();
        EmbedFromRequest(http.Requests.Single()).GetProperty("title").GetString().Should().Contain("test");
    }

    [Fact]
    public async Task Test_returns_failure_on_non_2xx()
    {
        var http = new FakeHttpClient().SetDefault(new HttpClientResponse(429, new Dictionary<string, string>(), ""));
        var result = await Build(http).OnTestAsync(default);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("429");
    }
}
