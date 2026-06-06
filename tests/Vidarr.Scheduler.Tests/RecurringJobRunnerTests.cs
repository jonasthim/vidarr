using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Contracts.Abstractions;
using Vidarr.Scheduler;

namespace Vidarr.Scheduler.Tests;

public class RecurringJobRunnerTests
{
    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class CountingJob : IRecurringJob
    {
        public string Name => "Counting";
        public TimeSpan Interval => TimeSpan.FromMinutes(15);
        public int Runs;
        public Task RunAsync(CancellationToken ct) { Runs++; return Task.CompletedTask; }
    }

    private sealed class ThrowingJob : IRecurringJob
    {
        public string Name => "Throwing";
        public TimeSpan Interval => TimeSpan.FromMinutes(15);
        public Task RunAsync(CancellationToken ct) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task RunByName_executes_job_and_records_success_history()
    {
        var clock = new FakeClock();
        var history = new InMemoryJobRunHistory();
        var job = new CountingJob();
        var sut = new RecurringJobRunner([job], history, clock, NullLogger<RecurringJobRunner>.Instance);

        await sut.RunByNameAsync("Counting", default);
        job.Runs.Should().Be(1);
        var run = history.Recent("Counting").Should().ContainSingle().Which;
        run.Succeeded.Should().BeTrue();
        run.FinishedAt.Should().NotBeNull();
        history.LastSuccessful("Counting").Should().NotBeNull();
    }

    [Fact]
    public async Task RunByName_records_failure_when_job_throws()
    {
        var clock = new FakeClock();
        var history = new InMemoryJobRunHistory();
        var sut = new RecurringJobRunner([new ThrowingJob()], history, clock, NullLogger<RecurringJobRunner>.Instance);

        await sut.RunByNameAsync("Throwing", default);
        var run = history.Recent("Throwing").Should().ContainSingle().Which;
        run.Succeeded.Should().BeFalse();
        run.FailureReason.Should().Be("boom");
    }

    [Fact]
    public async Task RunByName_unknown_job_is_silent_no_op()
    {
        var sut = new RecurringJobRunner([], new InMemoryJobRunHistory(),
            new FakeClock(), NullLogger<RecurringJobRunner>.Instance);
        await sut.RunByNameAsync("Mystery", default);
    }

    [Fact]
    public async Task RunByName_is_case_insensitive()
    {
        var job = new CountingJob();
        var sut = new RecurringJobRunner([job], new InMemoryJobRunHistory(),
            new FakeClock(), NullLogger<RecurringJobRunner>.Instance);
        await sut.RunByNameAsync("counting", default);
        job.Runs.Should().Be(1);
    }

    [Fact]
    public void ListJobs_reports_name_interval_and_last_success()
    {
        var clock = new FakeClock();
        var history = new InMemoryJobRunHistory();
        history.RecordStart("Counting", clock.UtcNow);
        history.RecordEnd("Counting", clock.UtcNow.AddSeconds(1), true, null);

        var sut = new RecurringJobRunner([new CountingJob()], history, clock, NullLogger<RecurringJobRunner>.Instance);
        var list = sut.ListJobs();
        list.Should().ContainSingle();
        list[0].Name.Should().Be("Counting");
        list[0].Interval.Should().Be(TimeSpan.FromMinutes(15));
        list[0].LastRun.Should().NotBeNull();
    }
}

public class InMemoryJobRunHistoryTests
{
    [Fact]
    public void Records_eviction_when_capacity_exceeded()
    {
        var history = new InMemoryJobRunHistory();
        var t = new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 600; i++)
        {
            history.RecordStart($"Job-{i}", t.AddSeconds(i));
            history.RecordEnd($"Job-{i}", t.AddSeconds(i + 1), true, null);
        }
        history.Recent(take: 1000).Should().HaveCountLessOrEqualTo(500);
    }

    [Fact]
    public void RecordEnd_with_no_open_run_appends_synthetic_row()
    {
        var history = new InMemoryJobRunHistory();
        history.RecordEnd("Solo", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), true, null);
        history.Recent().Should().ContainSingle().Which.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Recent_can_filter_by_job_name()
    {
        var history = new InMemoryJobRunHistory();
        history.RecordStart("A", DateTimeOffset.UtcNow);
        history.RecordEnd("A", DateTimeOffset.UtcNow, true, null);
        history.RecordStart("B", DateTimeOffset.UtcNow);
        history.RecordEnd("B", DateTimeOffset.UtcNow, true, null);
        history.Recent("A").Should().ContainSingle().Which.JobName.Should().Be("A");
    }

    [Fact]
    public void LastSuccessful_returns_null_when_no_success_yet()
    {
        var history = new InMemoryJobRunHistory();
        history.RecordStart("X", DateTimeOffset.UtcNow);
        history.RecordEnd("X", DateTimeOffset.UtcNow, false, "nope");
        history.LastSuccessful("X").Should().BeNull();
    }
}
