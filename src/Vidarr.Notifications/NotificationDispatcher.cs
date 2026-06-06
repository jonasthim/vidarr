using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.Contracts.Models;

namespace Vidarr.Notifications;

/// <summary>
/// Subscribes to the in-process <see cref="IEventBus"/> on construction; for every event
/// fan-outs to each <see cref="INotification"/> whose <see cref="INotification.SupportedEvents"/>
/// matches. Per-notifier exceptions are isolated, logged, and re-published as a
/// <see cref="HealthIssueEvent"/> so the UI shows the failure prominently.
/// Disposing the dispatcher releases the bus subscriptions.
/// </summary>
public sealed class NotificationDispatcher : IDisposable
{
    private readonly IEventBus _bus;
    private readonly Func<IReadOnlyList<INotification>> _notifierFactory;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly List<IDisposable> _subscriptions = [];

    public NotificationDispatcher(
        IEventBus bus,
        Func<IReadOnlyList<INotification>> notifierFactory,
        ILogger<NotificationDispatcher> logger)
    {
        _bus = bus;
        _notifierFactory = notifierFactory;
        _logger = logger;

        _subscriptions.Add(_bus.Subscribe<GrabEvent>((e, ct) => FanOutAsync(e, NotificationEventType.OnGrab, (n, _ct) => n.OnGrabAsync(e, _ct), ct)));
        _subscriptions.Add(_bus.Subscribe<ImportEvent>((e, ct) => FanOutAsync(e, NotificationEventType.OnImport, (n, _ct) => n.OnImportAsync(e, _ct), ct)));
        _subscriptions.Add(_bus.Subscribe<UpgradeEvent>((e, ct) => FanOutAsync(e, NotificationEventType.OnUpgrade, (n, _ct) => n.OnUpgradeAsync(e, _ct), ct)));
        _subscriptions.Add(_bus.Subscribe<DeleteEvent>((e, ct) => FanOutAsync(e, NotificationEventType.OnDelete, (n, _ct) => n.OnDeleteAsync(e, _ct), ct)));
        _subscriptions.Add(_bus.Subscribe<HealthIssueEvent>((e, ct) => FanOutAsync(e, NotificationEventType.OnHealthIssue, (n, _ct) => n.OnHealthIssueAsync(e, _ct), ct)));
    }

    public void Dispose()
    {
        foreach (var s in _subscriptions) s.Dispose();
        _subscriptions.Clear();
    }

    internal async Task FanOutAsync<TEvent>(
        TEvent evt,
        NotificationEventType eventType,
        Func<INotification, CancellationToken, Task> invoker,
        CancellationToken ct)
        where TEvent : class
    {
        var notifiers = _notifierFactory();
        foreach (var notifier in notifiers)
        {
            ct.ThrowIfCancellationRequested();
            if (!notifier.SupportedEvents.Contains(eventType))
            {
                continue;
            }
            try
            {
                await invoker(notifier, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Notification {Notifier} failed on {Event}", notifier.Name, eventType);
                if (eventType != NotificationEventType.OnHealthIssue)
                {
                    // Don't recurse infinitely if the health-issue handler itself throws.
                    await _bus.PublishAsync(new HealthIssueEvent(
                        OccurredAt: DateTimeOffset.UtcNow,
                        Source: $"Notification/{notifier.Name}",
                        Severity: HealthSeverity.Warning,
                        Message: $"{eventType}: {ex.Message}",
                        Resolved: false), ct);
                }
            }
        }
    }
}
