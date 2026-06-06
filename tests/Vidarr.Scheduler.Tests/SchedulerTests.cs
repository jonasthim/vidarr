using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Scheduler;

namespace Vidarr.Scheduler.Tests;

public sealed record StubCommand(string Payload) : ICommand
{
    public string Name => "Stub";
}

public sealed class StubHandler : ICommandHandler<StubCommand>
{
    public List<StubCommand> Received { get; } = [];
    public bool ShouldThrow { get; set; }

    public Task HandleAsync(StubCommand command, CancellationToken ct)
    {
        if (ShouldThrow) throw new InvalidOperationException("boom");
        Received.Add(command);
        return Task.CompletedTask;
    }
}

public class SchedulerTests
{
    [Fact]
    public async Task ChannelCommandQueue_enqueues_and_dequeues_in_order()
    {
        var sut = new ChannelCommandQueue();
        var enqueued = new[] { new StubCommand("a"), new StubCommand("b"), new StubCommand("c") };

        foreach (var c in enqueued)
        {
            await sut.EnqueueAsync(c, default);
        }

        var received = new List<ICommand>();
        using var cts = new CancellationTokenSource();
        try
        {
            await foreach (var c in sut.DequeueAsync(cts.Token))
            {
                received.Add(c);
                if (received.Count == enqueued.Length) await cts.CancelAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected once we've consumed all queued items.
        }

        received.Should().HaveCount(3);
        received.Cast<StubCommand>().Select(c => c.Payload).Should().Equal("a", "b", "c");
    }

    [Fact]
    public async Task CommandDispatcher_invokes_registered_handler()
    {
        var handler = new StubHandler();
        var services = new ServiceCollection()
            .AddSingleton<ICommandHandler<StubCommand>>(handler)
            .BuildServiceProvider();

        var sut = new CommandDispatcher(services, NullLogger<CommandDispatcher>.Instance);
        await sut.DispatchAsync(new StubCommand("x"), default);

        handler.Received.Should().HaveCount(1);
        handler.Received[0].Payload.Should().Be("x");
    }

    [Fact]
    public async Task CommandDispatcher_logs_and_swallows_when_handler_throws()
    {
        var handler = new StubHandler { ShouldThrow = true };
        var services = new ServiceCollection()
            .AddSingleton<ICommandHandler<StubCommand>>(handler)
            .BuildServiceProvider();

        var sut = new CommandDispatcher(services, NullLogger<CommandDispatcher>.Instance);
        await sut.DispatchAsync(new StubCommand("x"), default);
        handler.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task CommandDispatcher_no_op_when_no_handler_registered()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new CommandDispatcher(services, NullLogger<CommandDispatcher>.Instance);
        await sut.DispatchAsync(new StubCommand("x"), default);
    }

    [Fact]
    public async Task CommandDispatcher_null_command_throws()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new CommandDispatcher(services, NullLogger<CommandDispatcher>.Instance);
        await FluentActions.Invoking(() => sut.DispatchAsync(null!, default))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Command_names_round_trip()
    {
        new ArtistSearchCommand(1).Name.Should().Be("ArtistSearch");
        new RefreshArtistMetadataCommand(1).Name.Should().Be("RefreshArtistMetadata");
        new SearchMusicVideoCommand(1).Name.Should().Be("SearchMusicVideo");
    }
}
