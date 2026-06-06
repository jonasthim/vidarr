using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vidarr.Scheduler;

public interface ICommandDispatcher
{
    Task DispatchAsync(ICommand command, CancellationToken ct);
}

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(IServiceProvider services, ILogger<CommandDispatcher> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task DispatchAsync(ICommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());

        using var scope = _services.CreateScope();
        var handler = scope.ServiceProvider.GetService(handlerType);
        if (handler is null)
        {
            _logger.LogWarning("No handler registered for command {Command}.", command.Name);
            return;
        }

        var method = handlerType.GetMethod("HandleAsync");
        try
        {
            await (Task)method!.Invoke(handler, [command, ct])!;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Command {Command} handler threw.", command.Name);
        }
    }
}
