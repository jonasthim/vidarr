using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Events;
using Vidarr.EventBus;

namespace Vidarr.Health.Tests;

public class HealthMonitorTests
{
    [Fact]
    public async Task Empty_check_set_yields_no_active_issues_and_no_events()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var observed = new List<HealthIssueEvent>();
        bus.Subscribe<HealthIssueEvent>((e, _) => { observed.Add(e); return Task.CompletedTask; });

        var monitor = new HealthMonitor([], bus, NullLogger<HealthMonitor>.Instance);
        var status = await monitor.RunAllAsync(default);

        status.Active.Should().BeEmpty();
        observed.Should().BeEmpty();
    }

    [Fact]
    public async Task Raises_event_on_first_appearance_and_no_event_on_unchanged_repeat()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var raised = new List<HealthIssueEvent>();
        bus.Subscribe<HealthIssueEvent>((e, _) => { raised.Add(e); return Task.CompletedTask; });

        var stub = new StubCheck("Stub",
            [new HealthIssue(new HealthIssueId("Stub", "src"), HealthSeverity.Warning, "msg")]);
        var monitor = new HealthMonitor([stub], bus, NullLogger<HealthMonitor>.Instance);

        await monitor.RunAllAsync(default);
        await monitor.RunAllAsync(default);

        raised.Should().ContainSingle(e => !e.Resolved);
        monitor.CurrentStatus().Active.Should().ContainSingle();
    }

    [Fact]
    public async Task Resolves_issue_when_check_no_longer_surfaces_it()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var events = new List<HealthIssueEvent>();
        bus.Subscribe<HealthIssueEvent>((e, _) => { events.Add(e); return Task.CompletedTask; });

        var stub = new StubCheck("Stub",
            [new HealthIssue(new HealthIssueId("Stub", "src"), HealthSeverity.Warning, "msg")]);
        var monitor = new HealthMonitor([stub], bus, NullLogger<HealthMonitor>.Instance);

        await monitor.RunAllAsync(default);
        stub.NextIssues = [];
        await monitor.RunAllAsync(default);

        events.Should().HaveCount(2);
        events[1].Resolved.Should().BeTrue();
        monitor.CurrentStatus().Active.Should().BeEmpty();
    }

    [Fact]
    public async Task Re_raises_when_message_changes()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var events = new List<HealthIssueEvent>();
        bus.Subscribe<HealthIssueEvent>((e, _) => { events.Add(e); return Task.CompletedTask; });

        var stub = new StubCheck("Stub",
            [new HealthIssue(new HealthIssueId("Stub", "src"), HealthSeverity.Warning, "v1")]);
        var monitor = new HealthMonitor([stub], bus, NullLogger<HealthMonitor>.Instance);
        await monitor.RunAllAsync(default);

        stub.NextIssues = [new HealthIssue(new HealthIssueId("Stub", "src"), HealthSeverity.Warning, "v2")];
        await monitor.RunAllAsync(default);

        events.Should().HaveCount(2);
        events.Should().AllSatisfy(e => e.Resolved.Should().BeFalse());
    }

    [Fact]
    public async Task Crashing_check_becomes_an_error_issue()
    {
        var bus = new InProcessEventBus(NullLogger<InProcessEventBus>.Instance);
        var crasher = new StubCheck("Crasher", []) { Throws = new InvalidOperationException("kaboom") };
        var monitor = new HealthMonitor([crasher], bus, NullLogger<HealthMonitor>.Instance);

        var status = await monitor.RunAllAsync(default);
        status.Active.Should().ContainSingle()
            .Which.Severity.Should().Be(HealthSeverity.Error);
        status.Active[0].Message.Should().Contain("kaboom");
    }

    [Fact]
    public void CurrentStatus_before_any_run_has_null_lastrun()
    {
        var monitor = new HealthMonitor([], new InProcessEventBus(NullLogger<InProcessEventBus>.Instance),
            NullLogger<HealthMonitor>.Instance);
        monitor.CurrentStatus().LastRun.Should().BeNull();
    }

    private sealed class StubCheck : IHealthCheck
    {
        public StubCheck(string name, IReadOnlyList<HealthIssue> issues)
        {
            Name = name;
            NextIssues = issues;
        }
        public string Name { get; }
        public IReadOnlyList<HealthIssue> NextIssues { get; set; }
        public Exception? Throws { get; set; }
        public Task<IReadOnlyList<HealthIssue>> RunAsync(CancellationToken ct) =>
            Throws is not null ? Task.FromException<IReadOnlyList<HealthIssue>>(Throws) : Task.FromResult(NextIssues);
    }
}
