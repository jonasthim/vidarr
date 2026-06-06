using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;
using Vidarr.EventBus;
using Vidarr.Notifications;

namespace Vidarr.Notifications.Tests;

public class NotificationDispatcherTests
{
    private static GrabEvent SampleGrab() =>
        new(DateTimeOffset.UtcNow, 1, [2], "Daft Punk - Around the World", "Newznab", "qBit", Quality.Webdl1080p);

    [Fact]
    public async Task Dispatcher_routes_event_to_subscribed_notifier()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var listener = new RecordingNotifier(new HashSet<NotificationEventType> { NotificationEventType.OnGrab });
        using var sut = new NotificationDispatcher(bus, () => [listener], NullLogger<NotificationDispatcher>.Instance);

        await bus.PublishAsync(SampleGrab(), default);
        listener.Grabs.Should().Be(1);
    }

    [Fact]
    public async Task Dispatcher_skips_notifier_when_event_type_not_in_supported_events()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var listener = new RecordingNotifier(new HashSet<NotificationEventType> { NotificationEventType.OnImport });
        using var sut = new NotificationDispatcher(bus, () => [listener], NullLogger<NotificationDispatcher>.Instance);

        await bus.PublishAsync(SampleGrab(), default);
        listener.Grabs.Should().Be(0);
    }

    [Fact]
    public async Task Failing_notifier_does_not_block_subsequent_notifiers()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var failing = new ThrowingNotifier(new HashSet<NotificationEventType> { NotificationEventType.OnGrab });
        var listener = new RecordingNotifier(new HashSet<NotificationEventType> { NotificationEventType.OnGrab });
        using var sut = new NotificationDispatcher(bus, () => [failing, listener], NullLogger<NotificationDispatcher>.Instance);

        await bus.PublishAsync(SampleGrab(), default);
        listener.Grabs.Should().Be(1);
    }

    [Fact]
    public async Task Failure_publishes_health_issue_event()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        HealthIssueEvent? captured = null;
        using var _ = bus.Subscribe<HealthIssueEvent>((e, _) => { captured = e; return Task.CompletedTask; });

        var failing = new ThrowingNotifier(new HashSet<NotificationEventType> { NotificationEventType.OnGrab });
        using var sut = new NotificationDispatcher(bus, () => [failing], NullLogger<NotificationDispatcher>.Instance);

        await bus.PublishAsync(SampleGrab(), default);
        captured.Should().NotBeNull();
        captured!.Source.Should().StartWith("Notification/");
        captured.Severity.Should().Be(HealthSeverity.Warning);
    }

    [Fact]
    public async Task Health_issue_handler_failure_does_not_recurse()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var failing = new ThrowingNotifier(new HashSet<NotificationEventType> { NotificationEventType.OnHealthIssue });
        var publishedCount = 0;
        using var _ = bus.Subscribe<HealthIssueEvent>((_, _) => { publishedCount++; return Task.CompletedTask; });
        using var sut = new NotificationDispatcher(bus, () => [failing], NullLogger<NotificationDispatcher>.Instance);

        // Publish a single health event. The failing notifier throws; the dispatcher must NOT
        // emit another health event (which would cause runaway recursion).
        await bus.PublishAsync(new HealthIssueEvent(DateTimeOffset.UtcNow, "x", HealthSeverity.Notice, "x", false), default);
        publishedCount.Should().Be(1);
    }

    [Fact]
    public async Task Disposing_dispatcher_removes_subscriptions()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var listener = new RecordingNotifier(new HashSet<NotificationEventType> { NotificationEventType.OnGrab });
        var sut = new NotificationDispatcher(bus, () => [listener], NullLogger<NotificationDispatcher>.Instance);

        await bus.PublishAsync(SampleGrab(), default);
        listener.Grabs.Should().Be(1);

        sut.Dispose();
        await bus.PublishAsync(SampleGrab(), default);
        listener.Grabs.Should().Be(1);
    }

    [Fact]
    public async Task Dispatcher_routes_import_upgrade_delete_health_paths()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var listener = new RecordingNotifier(new HashSet<NotificationEventType>
        {
            NotificationEventType.OnImport, NotificationEventType.OnUpgrade,
            NotificationEventType.OnDelete, NotificationEventType.OnHealthIssue,
        });
        using var sut = new NotificationDispatcher(bus, () => [listener], NullLogger<NotificationDispatcher>.Instance);

        await bus.PublishAsync(new ImportEvent(DateTimeOffset.UtcNow, 1, 2, "/x", 0, Quality.Webdl720p, null), default);
        await bus.PublishAsync(new UpgradeEvent(DateTimeOffset.UtcNow, 1, 2, "/x", Quality.Webdl720p, Quality.Webdl1080p), default);
        await bus.PublishAsync(new DeleteEvent(DateTimeOffset.UtcNow, 1, 2, "/x"), default);
        await bus.PublishAsync(new HealthIssueEvent(DateTimeOffset.UtcNow, "x", HealthSeverity.Notice, "x", false), default);

        listener.Imports.Should().Be(1);
        listener.Upgrades.Should().Be(1);
        listener.Deletes.Should().Be(1);
        listener.HealthIssues.Should().Be(1);
    }

    private sealed class RecordingNotifier : INotification
    {
        public RecordingNotifier(IReadOnlySet<NotificationEventType> events) { SupportedEvents = events; }
        public int Id => 0;
        public string Name => "Recorder";
        public IReadOnlySet<NotificationEventType> SupportedEvents { get; }
        public int Grabs, Imports, Upgrades, Deletes, HealthIssues;
        public Task OnGrabAsync(GrabEvent e, CancellationToken ct) { Grabs++; return Task.CompletedTask; }
        public Task OnImportAsync(ImportEvent e, CancellationToken ct) { Imports++; return Task.CompletedTask; }
        public Task OnUpgradeAsync(UpgradeEvent e, CancellationToken ct) { Upgrades++; return Task.CompletedTask; }
        public Task OnDeleteAsync(DeleteEvent e, CancellationToken ct) { Deletes++; return Task.CompletedTask; }
        public Task OnHealthIssueAsync(HealthIssueEvent e, CancellationToken ct) { HealthIssues++; return Task.CompletedTask; }
        public Task<NotificationTestResult> OnTestAsync(CancellationToken ct) => Task.FromResult(new NotificationTestResult(true, null));
    }

    private sealed class ThrowingNotifier : INotification
    {
        public ThrowingNotifier(IReadOnlySet<NotificationEventType> events) { SupportedEvents = events; }
        public int Id => 1;
        public string Name => "Thrower";
        public IReadOnlySet<NotificationEventType> SupportedEvents { get; }
        public Task OnGrabAsync(GrabEvent e, CancellationToken ct) => throw new InvalidOperationException("grab failed");
        public Task OnImportAsync(ImportEvent e, CancellationToken ct) => throw new InvalidOperationException("import failed");
        public Task OnUpgradeAsync(UpgradeEvent e, CancellationToken ct) => throw new InvalidOperationException("upgrade failed");
        public Task OnDeleteAsync(DeleteEvent e, CancellationToken ct) => throw new InvalidOperationException("delete failed");
        public Task OnHealthIssueAsync(HealthIssueEvent e, CancellationToken ct) => throw new InvalidOperationException("health failed");
        public Task<NotificationTestResult> OnTestAsync(CancellationToken ct) => throw new InvalidOperationException("test failed");
    }
}
