using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Vidarr.Scheduler;

public interface ICommand
{
    string Name { get; }
}

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken ct);
}

public interface ICommandQueue
{
    ValueTask EnqueueAsync(ICommand command, CancellationToken ct);
    IAsyncEnumerable<ICommand> DequeueAsync(CancellationToken ct);
}

public sealed class ChannelCommandQueue : ICommandQueue
{
    private readonly Channel<ICommand> _channel = Channel.CreateUnbounded<ICommand>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
    });

    public ValueTask EnqueueAsync(ICommand command, CancellationToken ct) =>
        _channel.Writer.WriteAsync(command, ct);

    public IAsyncEnumerable<ICommand> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

public sealed record ArtistSearchCommand(int ArtistId) : ICommand
{
    public string Name => "ArtistSearch";
}

public sealed record RefreshArtistMetadataCommand(int ArtistId) : ICommand
{
    public string Name => "RefreshArtistMetadata";
}

public sealed record SearchMusicVideoCommand(int MusicVideoId) : ICommand
{
    public string Name => "SearchMusicVideo";
}
