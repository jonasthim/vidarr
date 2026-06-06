using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Scheduler;

namespace Vidarr.Host.Jobs;

/// <summary>
/// Hourly per-process refresh that walks artists whose <c>LastInfoSync</c> is older
/// than <see cref="StaleAfter"/>, calls the metadata provider for the current track
/// list, and upserts new music videos as monitored entries on the wanted list.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Composition job; integration-tested via the runner.")]
public sealed class ArtistRefreshJob : IRecurringJob
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(6);

    private readonly IServiceProvider _services;
    private readonly ISystemClock _clock;
    private readonly ILogger<ArtistRefreshJob> _logger;

    public ArtistRefreshJob(IServiceProvider services, ISystemClock clock, ILogger<ArtistRefreshJob> logger)
    {
        _services = services;
        _clock = clock;
        _logger = logger;
    }

    public string Name => "ArtistRefresh";
    public TimeSpan Interval => TimeSpan.FromHours(1);

    public async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var artists = scope.ServiceProvider.GetRequiredService<IArtistRepository>();
        var videos = scope.ServiceProvider.GetRequiredService<IMusicVideoRepository>();
        var metadata = scope.ServiceProvider.GetRequiredService<IMetadataProvider>();

        var now = _clock.UtcNow;
        var all = await artists.ListAsync(ct);
        var stale = all.Where(a => a.LastInfoSync is null || (now - a.LastInfoSync.Value) > StaleAfter).ToList();
        if (stale.Count == 0)
        {
            _logger.LogDebug("ArtistRefresh: no stale artists ({Total} total)", all.Count);
            return;
        }

        _logger.LogInformation("ArtistRefresh: refreshing {Count} artists ({Skipped} still fresh)", stale.Count, all.Count - stale.Count);
        foreach (var artist in stale)
        {
            ct.ThrowIfCancellationRequested();
            string? imvdbId = null;
            try
            {
                var external = JsonSerializer.Deserialize<Dictionary<string, string>>(artist.ExternalIdsJson) ?? new();
                external.TryGetValue("imvdb", out imvdbId);
            }
            catch (JsonException) { /* ignore — leave imvdbId null */ }
            if (string.IsNullOrEmpty(imvdbId))
            {
                _logger.LogDebug("ArtistRefresh: artist {Id} has no IMVDb id", artist.Id);
                continue;
            }

            try
            {
                var providerVideos = await metadata.GetArtistVideosAsync(imvdbId, ct);
                var existing = (await videos.ListByArtistAsync(artist.Id, ct))
                    .Select(v => SafeExternalId(v.ExternalIdsJson, "imvdb"))
                    .Where(s => s is not null)
                    .ToHashSet(StringComparer.Ordinal);

                var added = 0;
                foreach (var p in providerVideos)
                {
                    if (existing.Contains(p.ProviderId)) continue;
                    await videos.AddAsync(new MusicVideo
                    {
                        ArtistId = artist.Id,
                        Title = p.Title,
                        Year = p.Year,
                        Type = p.Type,
                        Director = p.Director,
                        Monitored = artist.MonitorMode != Vidarr.Contracts.Models.MonitorMode.None,
                        ExternalIdsJson = JsonSerializer.Serialize(p.ExternalIds),
                        ThumbnailUrl = p.ThumbnailUrl?.AbsoluteUri,
                    }, ct);
                    added++;
                }

                artist.LastInfoSync = now;
                await artists.UpdateAsync(artist, ct);
                _logger.LogInformation("ArtistRefresh: {Artist} +{Added} new videos", artist.Name, added);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "ArtistRefresh failed for {Artist}", artist.Name);
            }
        }
    }

    private static string? SafeExternalId(string json, string key)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json)?.GetValueOrDefault(key); }
        catch (JsonException) { return null; }
    }
}
