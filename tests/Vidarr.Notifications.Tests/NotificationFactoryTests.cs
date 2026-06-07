using Vidarr.Contracts.Models;
using Vidarr.Notifications;
using Vidarr.Tests.Common;

namespace Vidarr.Notifications.Tests;

public class NotificationFactoryTests
{
    private static FakeHttpClient Http() => new();
    private static readonly HashSet<NotificationEventType> AllEvents =
        new(Enum.GetValues<NotificationEventType>());

    [Fact]
    public void Webhook_factory_schema_and_create()
    {
        var f = new WebhookFactory(Http());
        f.Implementation.Should().Be("Webhook");
        f.DisplayName.Should().NotBeEmpty();
        f.SettingsSchema.Should().NotBeEmpty();
        f.SupportedEvents.Should().NotBeEmpty();
        f.Create(1, "wh", "{\"url\":\"https://example.invalid/hook\"}", AllEvents).Should().NotBeNull();
        f.Create(2, "wh-default", "{}", AllEvents).Should().NotBeNull();
    }

    [Fact]
    public void Plex_factory_create()
    {
        var f = new PlexFactory(Http());
        f.Implementation.Should().Be("Plex");
        f.SupportedEvents.Should().Contain(NotificationEventType.OnImport);
        f.Create(1, "plex", "{\"baseUrl\":\"http://plex:32400\",\"token\":\"tk\",\"librarySectionId\":3}", AllEvents).Should().NotBeNull();
        f.Create(2, "plex-default", "{}", AllEvents).Should().NotBeNull();
    }

    [Fact]
    public void Jellyfin_factory_create()
    {
        var f = new JellyfinFactory(Http());
        f.Implementation.Should().Be("Jellyfin");
        f.SupportedEvents.Should().Contain(NotificationEventType.OnDelete);
        f.Create(1, "jf", "{\"baseUrl\":\"http://jf:8096\",\"apiKey\":\"k\"}", AllEvents).Should().NotBeNull();
        f.Create(2, "jf-default", "{}", AllEvents).Should().NotBeNull();
    }

    [Fact]
    public void Discord_factory_create()
    {
        var f = new DiscordFactory(Http());
        f.Implementation.Should().Be("Discord");
        f.SupportedEvents.Should().Contain(NotificationEventType.OnGrab);
        f.Create(1, "dc", "{\"webhookUrl\":\"https://discord.com/api/webhooks/x/y\",\"username\":\"bot\",\"avatarUrl\":\"https://x\"}", AllEvents).Should().NotBeNull();
        f.Create(2, "dc-default", "{}", AllEvents).Should().NotBeNull();
    }
}
