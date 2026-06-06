using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Api;
using Vidarr.Contracts.Abstractions;
using Vidarr.Scheduler;
using Vidarr.Tests.Common;

namespace Vidarr.Api.Tests;

public class SystemCommandEndpointsTests : IDisposable
{
    private readonly IHost _host;
    private readonly HttpClient _client;
    private readonly CountingJob _job = new();

    public SystemCommandEndpointsTests()
    {
        _host = new HostBuilder().ConfigureWebHost(web =>
        {
            web.UseTestServer();
            web.ConfigureServices(s =>
            {
                s.AddRouting();
                s.AddSingleton<ISystemClock, FakeClock>();
                s.AddSingleton<IRecurringJob>(_job);
                s.AddSingleton<IJobRunHistory, InMemoryJobRunHistory>();
                s.AddSingleton<IRecurringJobRunner>(sp => new RecurringJobRunner(
                    sp.GetServices<IRecurringJob>(),
                    sp.GetRequiredService<IJobRunHistory>(),
                    sp.GetRequiredService<ISystemClock>(),
                    NullLogger<RecurringJobRunner>.Instance));
            });
            web.Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(e => e.MapVidarrSystemCommandApi());
            });
        }).Start();
        _client = _host.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task System_command_lists_registered_jobs()
    {
        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/system/command"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var commands = await resp.Content.ReadFromJsonAsync<SystemCommandDto[]>();
        commands.Should().NotBeNull();
        commands!.Should().ContainSingle().Which.Name.Should().Be("Counting");
    }

    [Fact]
    public async Task Post_system_command_triggers_job_and_returns_202()
    {
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/system/command/Counting"), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Fire-and-forget: wait briefly for the background task to land.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (_job.Runs == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
        _job.Runs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Post_system_command_unknown_name_still_returns_202_but_records_nothing()
    {
        var resp = await _client.PostAsync(new Uri("http://localhost/api/v1/system/command/Mystery"), content: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await Task.Delay(50);
        _job.Runs.Should().Be(0);
    }

    [Fact]
    public async Task Job_runs_returns_history()
    {
        await _client.PostAsync(new Uri("http://localhost/api/v1/system/command/Counting"), content: null);
        await Task.Delay(100);

        var resp = await _client.GetAsync(new Uri("http://localhost/api/v1/system/jobs/runs?job=Counting"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var runs = await resp.Content.ReadFromJsonAsync<SystemCommandRunDto[]>();
        runs.Should().NotBeNull();
        runs!.Should().NotBeEmpty();
        runs![0].Succeeded.Should().BeTrue();
    }

    private sealed class CountingJob : IRecurringJob
    {
        public string Name => "Counting";
        public TimeSpan Interval => TimeSpan.FromMinutes(15);
        public int Runs;
        public Task RunAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Runs);
            return Task.CompletedTask;
        }
    }
}
