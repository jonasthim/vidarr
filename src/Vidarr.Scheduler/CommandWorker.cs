using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Vidarr.Scheduler;

[ExcludeFromCodeCoverage(Justification = "BackgroundService loop; integration-tested via WebApplicationFactory.")]
public sealed class CommandWorker : BackgroundService
{
    private readonly ICommandQueue _queue;
    private readonly ICommandDispatcher _dispatcher;
    private readonly ILogger<CommandWorker> _logger;

    public CommandWorker(ICommandQueue queue, ICommandDispatcher dispatcher, ILogger<CommandWorker> logger)
    {
        _queue = queue;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _queue.DequeueAsync(stoppingToken))
        {
            _logger.LogDebug("Dispatching command {Command}.", command.Name);
            await _dispatcher.DispatchAsync(command, stoppingToken);
        }
    }
}
