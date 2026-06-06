using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Vidarr.Catalog;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.Scheduler;

namespace Vidarr.Api;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapVidarrApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/system/status", (ISystemClock clock) =>
            Results.Ok(new SystemStatusDto("0.1.0", clock.UtcNow.ToString("O"), Authenticated: true)));

        v1.MapPost("/artist/lookup", async (ArtistLookupRequest req, IMetadataProvider metadata, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Query))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError(nameof(req.Query), "Query is required")]));
            }
            var results = await metadata.SearchArtistsAsync(req.Query, ct);
            return Results.Ok(results.Select(r => new ArtistLookupResult(
                r.ProviderId, r.Name, r.Disambiguation, r.Country, r.ThumbnailUrl?.AbsoluteUri)).ToArray());
        });

        v1.MapPost("/artist", async (AddArtistRequest req, IMetadataProvider metadata, IArtistRepository artists, CancellationToken ct) =>
        {
            if (!string.Equals(req.Provider, metadata.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError(nameof(req.Provider), $"Unknown provider {req.Provider}")]));
            }

            var existing = await artists.FindByExternalIdAsync(req.Provider, req.ProviderId, ct);
            if (existing is not null)
            {
                return Results.Conflict(new ApiErrorResponse([new ApiError("providerId", $"Artist {req.ProviderId} already added")]));
            }

            var details = await metadata.GetArtistAsync(req.ProviderId, ct);
            var artist = new Artist
            {
                Name = details.Name,
                SortName = details.SortName ?? details.Name,
                Country = details.Country,
                ExternalIdsJson = System.Text.Json.JsonSerializer.Serialize(details.ExternalIds),
                YouTubeChannelIdsJson = System.Text.Json.JsonSerializer.Serialize(details.YouTubeChannelIds),
                Monitored = true,
                MonitorMode = req.MonitorMode,
                QualityProfileId = req.QualityProfileId,
                RootFolderPath = req.RootFolderPath,
                Added = DateTimeOffset.UtcNow,
            };

            var created = await artists.AddAsync(artist, ct);
            return Results.Created($"/api/v1/artist/{created.Id}", ToDto(created));
        });

        v1.MapGet("/artist", async (IArtistRepository artists, CancellationToken ct) =>
        {
            var list = await artists.ListAsync(ct);
            return Results.Ok(list.Select(ToDto).ToArray());
        });

        v1.MapGet("/artist/{id:int}", async (int id, IArtistRepository artists, CancellationToken ct) =>
        {
            var artist = await artists.GetAsync(id, ct);
            return artist is null ? Results.NotFound() : Results.Ok(ToDto(artist));
        });

        v1.MapPut("/artist/{id:int}/youtube-channels", async (
            int id, YouTubeChannelsRequest req, IArtistRepository artists, CancellationToken ct) =>
        {
            var artist = await artists.GetAsync(id, ct);
            if (artist is null) return Results.NotFound();
            var cleaned = req.ChannelIds
                .Select(c => c?.Trim() ?? string.Empty)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            artist.YouTubeChannelIdsJson = System.Text.Json.JsonSerializer.Serialize(cleaned);
            await artists.UpdateAsync(artist, ct);
            return Results.Ok(ToDto(artist));
        });

        v1.MapGet("/musicvideo", async (int? artistId, IMusicVideoRepository videos, CancellationToken ct) =>
        {
            if (artistId is null)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("artistId", "artistId query is required")]));
            }
            var list = await videos.ListByArtistAsync(artistId.Value, ct);
            return Results.Ok(list.Select(ToDto).ToArray());
        });

        v1.MapPost("/command", async (CommandRequest req, ICommandQueue queue, CancellationToken ct) =>
        {
            ICommand cmd = req.Name switch
            {
                "ArtistSearch" when req.ArtistId is { } aid => new ArtistSearchCommand(aid),
                "RefreshArtistMetadata" when req.ArtistId is { } aid => new RefreshArtistMetadataCommand(aid),
                "SearchMusicVideo" when req.MusicVideoId is { } mvid => new SearchMusicVideoCommand(mvid),
                _ => null!,
            };
            if (cmd is null)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("name", $"Unknown or incomplete command {req.Name}")]));
            }
            await queue.EnqueueAsync(cmd, ct);
            return Results.Accepted(value: new CommandResponse("queued", cmd.Name));
        });

        // Remove a queue item. When ?blocklist=true the release title is added to the
        // blocklist so future Decision passes will reject it.
        v1.MapDelete("/queue/{id}", async (
            string id,
            IDownloadClient client,
            [Microsoft.AspNetCore.Mvc.FromServices] IBlocklistRepository blocklist,
            [Microsoft.AspNetCore.Mvc.FromQuery(Name = "blocklist")] bool? addToBlocklist,
            CancellationToken ct) =>
        {
            var items = await client.GetItemsAsync(ct);
            var item = items.FirstOrDefault(i => string.Equals(i.Id.Value, id, StringComparison.Ordinal));
            var shouldBlocklist = addToBlocklist == true;
            await client.RemoveAsync(new DownloadClientItemId(id), deleteData: shouldBlocklist, ct);
            if (shouldBlocklist && item is not null && !string.IsNullOrEmpty(item.Title))
            {
                await blocklist.AddAsync(new Vidarr.Catalog.Entities.BlocklistEntry
                {
                    ReleaseTitle = item.Title,
                    IndexerName = "manual",
                    Reason = "manual-blocklist",
                    Date = DateTimeOffset.UtcNow,
                }, ct);
            }
            return Results.NoContent();
        });

        v1.MapGet("/queue", async (
            IDownloadClient client,
            [Microsoft.AspNetCore.Mvc.FromServices] Vidarr.DownloadClients.IDownloadClientRegistry? registry,
            CancellationToken ct) =>
        {
            // Aggregate items from the in-DI default IDownloadClient plus every persisted
            // download-client config. The default client (yt-dlp) is queried first so its
            // entries appear before externally-managed torrents/usenet items.
            var all = new List<(DownloadClientItem Item, string ClientName)>();

            var defaultItems = await client.GetItemsAsync(ct);
            foreach (var i in defaultItems)
            {
                all.Add((i, client.Name));
            }

            if (registry is not null)
            {
                var active = await registry.GetActiveAsync(ct);
                foreach (var dc in active)
                {
                    try
                    {
                        var items = await dc.GetItemsAsync(ct);
                        foreach (var i in items) all.Add((i, dc.Name));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        all.Add((new DownloadClientItem(new DownloadClientItemId(string.Empty),
                            Title: $"<error from {dc.Name}>", TotalBytes: null, RemainingBytes: null,
                            Status: DownloadItemStatus.Failed, OutputPath: null, Eta: null,
                            Message: ex.Message), dc.Name));
                    }
                }
            }

            return Results.Ok(all.Select(t => new
            {
                id = t.Item.Id.Value,
                title = t.Item.Title,
                status = t.Item.Status.ToString(),
                totalBytes = t.Item.TotalBytes,
                remainingBytes = t.Item.RemainingBytes,
                etaSeconds = t.Item.Eta?.TotalSeconds,
                outputPath = t.Item.OutputPath,
                message = t.Item.Message,
                downloadClient = t.ClientName,
            }).ToArray());
        });

        return app;
    }

    internal static ArtistDto ToDto(Artist a) =>
        new(a.Id, a.Name, a.SortName, a.Country, a.Monitored, a.MonitorMode, a.RootFolderPath, a.Added, ParseChannelIds(a.YouTubeChannelIdsJson));

    private static string[] ParseChannelIds(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    internal static MusicVideoDto ToDto(MusicVideo v) =>
        new(v.Id, v.ArtistId, v.Title, v.Year, v.Type, v.Monitored, v.HasFile);
}
