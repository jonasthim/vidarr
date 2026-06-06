using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.EventBus;

namespace Vidarr.EventBus.Tests;

public sealed record TestEvent(string Payload);
public sealed record OtherEvent(int Value);

public class InProcessEventBusTests
{
    private static InProcessEventBus Build() => new(NullLogger<InProcessEventBus>.Instance);

    [Fact]
    public async Task Subscriber_receives_published_event()
    {
        var sut = Build();
        TestEvent? received = null;
        using var _ = sut.Subscribe<TestEvent>((e, _) => { received = e; return Task.CompletedTask; });

        await sut.PublishAsync(new TestEvent("hello"), default);

        received.Should().NotBeNull();
        received!.Payload.Should().Be("hello");
    }

    [Fact]
    public async Task Multiple_subscribers_all_receive_event()
    {
        var sut = Build();
        var count = 0;
        using var _1 = sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        using var _2 = sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        using var _3 = sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        await sut.PublishAsync(new TestEvent("x"), default);

        count.Should().Be(3);
    }

    [Fact]
    public async Task Disposing_subscription_removes_handler()
    {
        var sut = Build();
        var count = 0;
        var token = sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        token.Dispose();
        token.Dispose(); // idempotent

        await sut.PublishAsync(new TestEvent("x"), default);
        count.Should().Be(0);
    }

    [Fact]
    public async Task Failing_handler_does_not_block_other_handlers()
    {
        var sut = Build();
        var reached = false;
        using var _1 = sut.Subscribe<TestEvent>((_, _) => throw new InvalidOperationException("boom"));
        using var _2 = sut.Subscribe<TestEvent>((_, _) => { reached = true; return Task.CompletedTask; });

        await sut.PublishAsync(new TestEvent("x"), default);

        reached.Should().BeTrue();
    }

    [Fact]
    public async Task Publishing_event_with_no_subscribers_is_no_op()
    {
        var sut = Build();
        await sut.PublishAsync(new OtherEvent(42), default);
    }

    [Fact]
    public async Task Different_event_types_are_isolated()
    {
        var sut = Build();
        var testCount = 0;
        var otherCount = 0;
        using var _1 = sut.Subscribe<TestEvent>((_, _) => { Interlocked.Increment(ref testCount); return Task.CompletedTask; });
        using var _2 = sut.Subscribe<OtherEvent>((_, _) => { Interlocked.Increment(ref otherCount); return Task.CompletedTask; });

        await sut.PublishAsync(new TestEvent("a"), default);
        testCount.Should().Be(1);
        otherCount.Should().Be(0);
    }

    [Fact]
    public async Task Cancellation_inside_handler_is_propagated()
    {
        var sut = Build();
        using var _ = sut.Subscribe<TestEvent>((_, ct) => throw new OperationCanceledException(ct));

        await FluentActions.Invoking(() => sut.PublishAsync(new TestEvent("x"), new CancellationToken(canceled: true)))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Publishing_null_throws_argument_null()
    {
        var sut = Build();
        await FluentActions.Invoking(() => sut.PublishAsync<TestEvent>(null!, default)).Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Subscribing_null_throws_argument_null()
    {
        var sut = Build();
        FluentActions.Invoking(() => sut.Subscribe<TestEvent>(null!)).Should().Throw<ArgumentNullException>();
    }
}
