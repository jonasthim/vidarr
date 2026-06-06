using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Domain;

namespace Vidarr.EventBus;

public sealed class InProcessEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subs = new();
    private readonly ILogger<InProcessEventBus> _logger;

    public InProcessEventBus(ILogger<InProcessEventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (!_subs.TryGetValue(typeof(TEvent), out var handlers))
        {
            return;
        }

        Subscription[] snapshot;
        lock (handlers)
        {
            snapshot = [.. handlers];
        }

        foreach (var sub in snapshot)
        {
            try
            {
                await ((Func<TEvent, CancellationToken, Task>)sub.Handler)(evt, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Event handler {Subscription} threw while handling {Event}.", sub.Id, typeof(TEvent).Name);
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _subs.GetOrAdd(typeof(TEvent), _ => []);
        var sub = new Subscription(Guid.NewGuid(), handler);
        lock (list)
        {
            list.Add(sub);
        }
        return new SubscriptionToken(() =>
        {
            lock (list)
            {
                list.Remove(sub);
            }
        });
    }

    private sealed record Subscription(Guid Id, object Handler);

    private sealed class SubscriptionToken : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public SubscriptionToken(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _dispose();
        }
    }
}
