using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Models;
using Vidarr.Indexers;

namespace Vidarr.Api;

public static class ReleaseEndpoints
{
    public static IEndpointRouteBuilder MapVidarrReleaseApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/release", async (
            int? artistId,
            int? musicVideoId,
            string? query,
            IReleaseSearchService search,
            IArtistRepository artists,
            IMusicVideoRepository videos,
            CancellationToken ct) =>
        {
            IndexerSearchCriteria? criteria = null;
            if (artistId is { } aid && musicVideoId is { } mvid)
            {
                var artist = await artists.GetAsync(aid, ct);
                var video = await videos.GetAsync(mvid, ct);
                if (artist is null || video is null)
                {
                    return Results.NotFound();
                }
                criteria = new IndexerSearchCriteria(
                    Query: $"{artist.Name} {video.Title}",
                    ArtistName: artist.Name,
                    Title: video.Title,
                    Year: video.Year,
                    Categories: ["music-video"]);
            }
            else if (!string.IsNullOrWhiteSpace(query))
            {
                criteria = new IndexerSearchCriteria(query, null, null, null, ["music-video"]);
            }
            else
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("query", "Provide artistId+musicVideoId or query")]));
            }

            var result = await search.SearchAsync(criteria, ct);
            return Results.Ok(new ReleaseSearchResponse(
                Releases: result.Releases.Select(ToDto).ToArray(),
                Failures: result.Failures.Select(f => new ReleaseSearchFailureDto(f.IndexerId, f.IndexerName, f.Reason)).ToArray(),
                IndexersQueried: result.IndexersQueried));
        });

        v1.MapGet("/indexer/schema", (IEnumerable<IIndexerFactory> factories) =>
            Results.Ok(factories.Select(f => new IndexerSchemaDto(
                Implementation: f.Implementation,
                DisplayName: f.DisplayName,
                Fields: f.SettingsSchema.Select(s => new IndexerSchemaFieldDto(s.Name, s.Label, s.Type, s.Required, s.HelpText)).ToArray())).ToArray()));

        v1.MapPost("/indexer/test", async (
            IndexerTestRequest req,
            IEnumerable<IIndexerFactory> factories,
            CancellationToken ct) =>
        {
            var factory = factories.FirstOrDefault(f => string.Equals(f.Implementation, req.Implementation, StringComparison.OrdinalIgnoreCase));
            if (factory is null)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("implementation", $"Unknown implementation {req.Implementation}")]));
            }
            try
            {
                var indexer = factory.Create(0, "test", req.SettingsJson ?? "{}");
                var result = await indexer.TestAsync(ct);
                return Results.Ok(new IndexerTestResultDto(result.Success, result.Message));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Ok(new IndexerTestResultDto(false, ex.Message));
            }
        });

        return app;
    }

    internal static ReleaseInfoDto ToDto(ReleaseInfo r) => new(
        Title: r.Title,
        SourceUrl: r.SourceUrl.AbsoluteUri,
        Magnet: r.Magnet,
        SizeBytes: r.SizeBytes,
        PublishedAt: r.PublishedAt,
        Seeders: r.Seeders,
        Leechers: r.Leechers,
        Protocol: r.Protocol.ToString(),
        IndexerName: r.IndexerName,
        IndexerCategory: r.IndexerCategory);
}

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ReleaseInfoDto(
    string Title,
    string SourceUrl,
    string? Magnet,
    long? SizeBytes,
    DateTimeOffset? PublishedAt,
    int? Seeders,
    int? Leechers,
    string Protocol,
    string IndexerName,
    string? IndexerCategory);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ReleaseSearchResponse(
    IReadOnlyList<ReleaseInfoDto> Releases,
    IReadOnlyList<ReleaseSearchFailureDto> Failures,
    int IndexersQueried);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record ReleaseSearchFailureDto(int IndexerId, string IndexerName, string Reason);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record IndexerSchemaDto(string Implementation, string DisplayName, IReadOnlyList<IndexerSchemaFieldDto> Fields);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record IndexerSchemaFieldDto(string Name, string Label, string Type, bool Required, string? HelpText);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record IndexerTestRequest(string Implementation, string? SettingsJson);

[ExcludeFromCodeCoverage(Justification = "Plain transport DTOs.")]
public sealed record IndexerTestResultDto(bool Success, string? Message);
