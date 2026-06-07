using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Contracts.Abstractions;
using Vidarr.Scheduler;
using Vidarr.Tests.Common;

namespace Vidarr.Scheduler.Tests;

public class LoggerScopesTests
{
    [Fact]
    public void Artist_pushes_scope_with_artist_id()
    {
        var logger = new RecordingLogger();
        using (logger.Artist(42))
        {
            logger.LogInformation("hi");
        }
        logger.Entries.Should().ContainSingle()
            .Which.Scopes.Should().ContainSingle(s => s.Contains(new KeyValuePair<string, object>("ArtistId", 42)));
    }

    [Fact]
    public void Indexer_and_download_client_emit_named_properties()
    {
        var logger = new RecordingLogger();
        using (logger.Indexer("NZBGeek"))
        using (logger.DownloadClient("qBit"))
        {
            logger.LogInformation("event");
        }
        var scopes = logger.Entries.Single().Scopes;
        scopes.Should().Contain(s => s.Contains(new KeyValuePair<string, object>("IndexerName", "NZBGeek")));
        scopes.Should().Contain(s => s.Contains(new KeyValuePair<string, object>("DownloadClient", "qBit")));
    }

    [Fact]
    public void MusicVideo_scope_disposes_cleanly_outside()
    {
        var logger = new RecordingLogger();
        var scope = logger.MusicVideo(99);
        scope!.Dispose();
        logger.LogInformation("after-dispose");
        logger.Entries.Should().ContainSingle().Which.Scopes.Should().BeEmpty();
    }
}

public class RecurringJobRunnerScopeTests
{
    [Fact]
    public async Task Runner_pushes_job_name_scope_during_execution()
    {
        var logger = new RecordingLogger<RecurringJobRunner>();
        var runner = new RecurringJobRunner(
            [new EchoJob(logger)],
            new InMemoryJobRunHistory(),
            new FakeClock(),
            logger);

        await runner.RunByNameAsync("Echo", default);

        logger.Entries.Should().NotBeEmpty();
        logger.Entries.Should().Contain(e =>
            e.Scopes.Any(s => s.Contains(new KeyValuePair<string, object>("JobName", "Echo"))));
    }

    private sealed class EchoJob : IRecurringJob
    {
        private readonly ILogger _logger;
        public EchoJob(ILogger logger) { _logger = logger; }
        public string Name => "Echo";
        public TimeSpan Interval => TimeSpan.FromHours(1);
        public Task RunAsync(CancellationToken ct)
        {
            _logger.LogInformation("inside-job");
            return Task.CompletedTask;
        }
    }
}

internal class RecordingLogger : ILogger
{
    public List<RecordedEntry> Entries { get; } = [];
    private readonly Stack<IReadOnlyList<KeyValuePair<string, object>>> _scopes = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var list = state switch
        {
            IEnumerable<KeyValuePair<string, object>> kvs => kvs.ToList(),
            _ => [new KeyValuePair<string, object>("Scope", state)],
        };
        _scopes.Push(list);
        return new ScopeBag(() => _scopes.Pop());
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new RecordedEntry(logLevel, formatter(state, exception), [.. _scopes]));

    public sealed record RecordedEntry(LogLevel Level, string Message, IReadOnlyList<IReadOnlyList<KeyValuePair<string, object>>> Scopes);

    private sealed class ScopeBag : IDisposable
    {
        private readonly Action _pop;
        private bool _disposed;
        public ScopeBag(Action pop) { _pop = pop; }
        public void Dispose() { if (!_disposed) { _disposed = true; _pop(); } }
    }
}

internal sealed class RecordingLogger<T> : RecordingLogger, ILogger<T>
{
}
