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

        v1.MapGet("/queue", async (IDownloadClient client, CancellationToken ct) =>
        {
            var items = await client.GetItemsAsync(ct);
            return Results.Ok(items.Select(i => new
            {
                id = i.Id.Value,
                title = i.Title,
                status = i.Status.ToString(),
                totalBytes = i.TotalBytes,
                remainingBytes = i.RemainingBytes,
                etaSeconds = i.Eta?.TotalSeconds,
                outputPath = i.OutputPath,
                message = i.Message,
            }).ToArray());
        });

        return app;
    }

    internal static ArtistDto ToDto(Artist a) =>
        new(a.Id, a.Name, a.SortName, a.Country, a.Monitored, a.MonitorMode, a.RootFolderPath, a.Added);

    internal static MusicVideoDto ToDto(MusicVideo v) =>
        new(v.Id, v.ArtistId, v.Title, v.Year, v.Type, v.Monitored, v.HasFile);
}
