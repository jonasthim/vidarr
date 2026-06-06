using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.DownloadClients;
using Vidarr.Scheduler;

namespace Vidarr.Host.Jobs;

/// <summary>
/// Polls every configured download client (plus the default IDownloadClient instance)
/// to surface in-flight progress and detect completed downloads. The Importer hand-off
/// proper lands in later phases — this job today just consolidates statuses so the UI
/// has a live snapshot.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Composition job; integration-tested via the runner.")]
public sealed class DownloadStatusPollJob : IRecurringJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DownloadStatusPollJob> _logger;

    public DownloadStatusPollJob(IServiceProvider services, ILogger<DownloadStatusPollJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public string Name => "DownloadStatusPoll";
    public TimeSpan Interval => TimeSpan.FromSeconds(30);

    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var defaultClient = scope.ServiceProvider.GetRequiredService<IDownloadClient>();
        var registry = scope.ServiceProvider.GetService<IDownloadClientRegistry>();

        var byStatus = new Dictionary<DownloadItemStatus, int>();
        var sources = new List<(string Name, IReadOnlyList<DownloadClientItem> Items, string? Failure)>();

        sources.Add(await PollAsync(defaultClient, ct));
        if (registry is not null)
        {
            foreach (var client in await registry.GetActiveAsync(ct))
            {
                sources.Add(await PollAsync(client, ct));
            }
        }

        foreach (var (_, items, _) in sources)
        {
            foreach (var item in items)
            {
                byStatus[item.Status] = byStatus.GetValueOrDefault(item.Status) + 1;
            }
        }

        var failed = sources.Where(s => s.Failure is not null).ToList();
        if (failed.Count > 0)
        {
            _logger.LogWarning("DownloadStatusPoll: {Bad}/{Total} clients failed — {Names}",
                failed.Count, sources.Count, string.Join(", ", failed.Select(f => f.Name)));
        }
        if (byStatus.Count > 0)
        {
            _logger.LogInformation("DownloadStatusPoll: {Summary}",
                string.Join(", ", byStatus.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}")));
        }
    }

    private static async Task<(string Name, IReadOnlyList<DownloadClientItem> Items, string? Failure)> PollAsync(
        IDownloadClient client, CancellationToken ct)
    {
        try
        {
            return (client.Name, await client.GetItemsAsync(ct), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (client.Name, Array.Empty<DownloadClientItem>(), ex.Message);
        }
    }
}
