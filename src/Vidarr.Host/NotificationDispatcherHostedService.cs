using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Vidarr.Notifications;

namespace Vidarr.Host;

/// <summary>
/// Keeps the NotificationDispatcher alive for the lifetime of the host so its EventBus
/// subscriptions stay attached. Disposing the host triggers IDisposable on the dispatcher
/// which releases the subscriptions.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Composition glue; integration tested via the dispatcher.")]
public sealed class NotificationDispatcherHostedService : IHostedService, IDisposable
{
    private readonly NotificationDispatcher _dispatcher;

    public NotificationDispatcherHostedService(NotificationDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose() => _dispatcher.Dispose();
}
