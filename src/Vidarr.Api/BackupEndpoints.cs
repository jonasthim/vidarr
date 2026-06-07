using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.Backup;

namespace Vidarr.Api;

public static class BackupEndpoints
{
    public static IEndpointRouteBuilder MapVidarrBackupApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/system/backup");

        v1.MapGet("", async (IBackupService backups, CancellationToken ct) =>
        {
            var list = await backups.ListAsync(ct);
            return Results.Ok(list.Select(ToDto).ToArray());
        });

        v1.MapPost("", async (IBackupService backups, CancellationToken ct) =>
        {
            var artifact = await backups.CreateAsync(ct);
            return Results.Created($"/api/v1/system/backup/{Path.GetFileName(artifact.Path)}", ToDto(artifact));
        });

        v1.MapPost("/restore", async (HttpRequest req, IBackupService backups, CancellationToken ct) =>
        {
            try
            {
                var result = await backups.StageRestoreAsync(req.Body, ct);
                return Results.Accepted(value: new RestoreResultDto(
                    Path.GetFileName(result.SqliteStagedPath),
                    result.ConfigStagedPath is null ? null : Path.GetFileName(result.ConfigStagedPath),
                    result.RestartRequired));
            }
            catch (InvalidDataException ex)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("body", ex.Message)]));
            }
        }).Accepts<Stream>("application/zip");

        v1.MapDelete("/{fileName}", async (string fileName, IBackupService backups, CancellationToken ct) =>
        {
            try
            {
                await backups.DeleteAsync(fileName, ct);
                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("fileName", ex.Message)]));
            }
        });

        return app;
    }

    internal static BackupArtifactDto ToDto(BackupArtifact a) =>
        new(Path.GetFileName(a.Path), a.SizeBytes, a.CreatedAt);
}

public sealed record BackupArtifactDto(string FileName, long SizeBytes, DateTimeOffset CreatedAt);
public sealed record RestoreResultDto(string StagedSqlite, string? StagedConfig, bool RestartRequired);
