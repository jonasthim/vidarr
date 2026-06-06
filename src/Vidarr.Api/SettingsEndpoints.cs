using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Models;

namespace Vidarr.Api;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapVidarrSettingsApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        // Quality definitions (system table; read-only).
        v1.MapGet("/qualitydefinition", () =>
            Results.Ok(Quality.All.Select(q => new QualityDto(q.Id, q.Name, q.Resolution.ToString(), q.Source.ToString())).ToArray()));

        // Tags
        v1.MapGet("/tag", async (ITagRepository repo, CancellationToken ct) =>
            Results.Ok((await repo.ListAsync(ct)).Select(t => new TagDto(t.Id, t.Label)).ToArray()));
        v1.MapPost("/tag", async (TagRequest req, ITagRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Label))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("label", "Label is required")]));
            }
            var tag = await repo.AddAsync(new Tag { Label = req.Label.Trim() }, ct);
            return Results.Created($"/api/v1/tag/{tag.Id}", new TagDto(tag.Id, tag.Label));
        });
        v1.MapDelete("/tag/{id:int}", async (int id, ITagRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        // Quality Profiles
        v1.MapGet("/qualityprofile", async (IQualityProfileRepository repo, CancellationToken ct) =>
            Results.Ok((await repo.ListAsync(ct)).Select(ToDto).ToArray()));
        v1.MapGet("/qualityprofile/{id:int}", async (int id, IQualityProfileRepository repo, CancellationToken ct) =>
        {
            var p = await repo.GetAsync(id, ct);
            return p is null ? Results.NotFound() : Results.Ok(ToDto(p));
        });
        v1.MapPost("/qualityprofile", async (QualityProfileRequest req, IQualityProfileRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name) || req.AllowedQualityIds.Count == 0)
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("name", "Name and at least one allowed quality are required")]));
            }
            var created = await repo.AddAsync(FromRequest(req), ct);
            return Results.Created($"/api/v1/qualityprofile/{created.Id}", ToDto(created));
        });
        v1.MapPut("/qualityprofile/{id:int}", async (int id, QualityProfileRequest req, IQualityProfileRepository repo, CancellationToken ct) =>
        {
            var existing = await repo.GetAsync(id, ct);
            if (existing is null) return Results.NotFound();
            existing.Name = req.Name;
            existing.AllowedQualityIdsJson = JsonSerializer.Serialize(req.AllowedQualityIds);
            existing.CutoffQualityId = req.CutoffQualityId;
            existing.UpgradeAllowed = req.UpgradeAllowed;
            existing.MinFormatScore = req.MinFormatScore;
            existing.MinSizeBytes = req.MinSizeBytes;
            existing.MaxSizeBytes = req.MaxSizeBytes;
            existing.TagsJson = JsonSerializer.Serialize(req.Tags);
            await repo.UpdateAsync(existing, ct);
            return Results.Ok(ToDto(existing));
        });
        v1.MapDelete("/qualityprofile/{id:int}", async (int id, IQualityProfileRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        // Custom Formats
        v1.MapGet("/customformat", async (ICustomFormatRepository repo, CancellationToken ct) =>
            Results.Ok((await repo.ListAsync(ct)).Select(f => new CustomFormatDto(f.Id, f.Name, f.IncludeCustomFormatWhenRenaming, f.SpecificationsJson)).ToArray()));
        v1.MapGet("/customformat/{id:int}", async (int id, ICustomFormatRepository repo, CancellationToken ct) =>
        {
            var f = await repo.GetAsync(id, ct);
            return f is null ? Results.NotFound() : Results.Ok(new CustomFormatDto(f.Id, f.Name, f.IncludeCustomFormatWhenRenaming, f.SpecificationsJson));
        });
        v1.MapPost("/customformat", async (CustomFormatRequest req, ICustomFormatRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("name", "Name is required")]));
            }
            var created = await repo.AddAsync(new CustomFormat
            {
                Name = req.Name.Trim(),
                IncludeCustomFormatWhenRenaming = req.IncludeCustomFormatWhenRenaming,
                SpecificationsJson = req.SpecificationsJson ?? "[]",
            }, ct);
            return Results.Created($"/api/v1/customformat/{created.Id}",
                new CustomFormatDto(created.Id, created.Name, created.IncludeCustomFormatWhenRenaming, created.SpecificationsJson));
        });
        v1.MapPut("/customformat/{id:int}", async (int id, CustomFormatRequest req, ICustomFormatRepository repo, CancellationToken ct) =>
        {
            var f = await repo.GetAsync(id, ct);
            if (f is null) return Results.NotFound();
            f.Name = req.Name;
            f.IncludeCustomFormatWhenRenaming = req.IncludeCustomFormatWhenRenaming;
            f.SpecificationsJson = req.SpecificationsJson ?? "[]";
            await repo.UpdateAsync(f, ct);
            return Results.Ok(new CustomFormatDto(f.Id, f.Name, f.IncludeCustomFormatWhenRenaming, f.SpecificationsJson));
        });
        v1.MapDelete("/customformat/{id:int}", async (int id, ICustomFormatRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        // Blocklist
        v1.MapGet("/blocklist", async (IBlocklistRepository repo, CancellationToken ct) =>
            Results.Ok((await repo.ListAsync(ct)).Select(b =>
                new BlocklistDto(b.Id, b.ArtistId, b.MusicVideoId, b.ReleaseTitle, b.IndexerName, b.Reason, b.Date)).ToArray()));
        v1.MapPost("/blocklist", async (BlocklistRequest req, IBlocklistRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.ReleaseTitle))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("releaseTitle", "ReleaseTitle is required")]));
            }
            var b = await repo.AddAsync(new BlocklistEntry
            {
                ArtistId = req.ArtistId,
                MusicVideoId = req.MusicVideoId,
                ReleaseTitle = req.ReleaseTitle,
                IndexerName = req.IndexerName ?? string.Empty,
                Reason = req.Reason,
                Date = DateTimeOffset.UtcNow,
            }, ct);
            return Results.Created($"/api/v1/blocklist/{b.Id}",
                new BlocklistDto(b.Id, b.ArtistId, b.MusicVideoId, b.ReleaseTitle, b.IndexerName, b.Reason, b.Date));
        });
        v1.MapDelete("/blocklist/{id:int}", async (int id, IBlocklistRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        // History (list-only)
        v1.MapGet("/history", async (int? artistId, int? musicVideoId, int? take, IHistoryRepository repo, CancellationToken ct) =>
        {
            var entries = await repo.ListAsync(artistId, musicVideoId, take ?? 50, ct);
            return Results.Ok(entries.Select(h => new HistoryDto(
                h.Id, h.EventType.ToString(), h.Date, h.ArtistId, h.MusicVideoId,
                h.ReleaseTitle, h.IndexerName, h.DownloadClientName, h.QualityId, h.DataJson)).ToArray());
        });

        // Indexer config
        MapConfigCrud<IndexerConfig, IndexerConfigRequest, IndexerConfigDto, IIndexerConfigRepository>(
            v1, "indexer",
            (req, ct) => new IndexerConfig
            {
                Name = req.Name,
                Implementation = req.Implementation,
                SettingsJson = req.SettingsJson ?? "{}",
                Priority = req.Priority,
                EnableRss = req.EnableRss,
                EnableAutomaticSearch = req.EnableAutomaticSearch,
                EnableInteractiveSearch = req.EnableInteractiveSearch,
                PreferredDownloadClientId = req.PreferredDownloadClientId,
                TagsJson = JsonSerializer.Serialize(req.Tags ?? []),
            },
            (existing, req) =>
            {
                existing.Name = req.Name;
                existing.Implementation = req.Implementation;
                existing.SettingsJson = req.SettingsJson ?? "{}";
                existing.Priority = req.Priority;
                existing.EnableRss = req.EnableRss;
                existing.EnableAutomaticSearch = req.EnableAutomaticSearch;
                existing.EnableInteractiveSearch = req.EnableInteractiveSearch;
                existing.PreferredDownloadClientId = req.PreferredDownloadClientId;
                existing.TagsJson = JsonSerializer.Serialize(req.Tags ?? []);
            },
            e => new IndexerConfigDto(e.Id, e.Name, e.Implementation, e.SettingsJson,
                e.Priority, e.EnableRss, e.EnableAutomaticSearch, e.EnableInteractiveSearch,
                e.PreferredDownloadClientId, ParseInts(e.TagsJson)));

        // Download client config
        MapConfigCrud<DownloadClientConfig, DownloadClientConfigRequest, DownloadClientConfigDto, IDownloadClientConfigRepository>(
            v1, "downloadclient",
            (req, ct) => new DownloadClientConfig
            {
                Name = req.Name,
                Implementation = req.Implementation,
                SettingsJson = req.SettingsJson ?? "{}",
                Priority = req.Priority,
                Enable = req.Enable,
                Category = req.Category,
                RemovesCompletedDownloads = req.RemovesCompletedDownloads,
                TagsJson = JsonSerializer.Serialize(req.Tags ?? []),
            },
            (existing, req) =>
            {
                existing.Name = req.Name;
                existing.Implementation = req.Implementation;
                existing.SettingsJson = req.SettingsJson ?? "{}";
                existing.Priority = req.Priority;
                existing.Enable = req.Enable;
                existing.Category = req.Category;
                existing.RemovesCompletedDownloads = req.RemovesCompletedDownloads;
                existing.TagsJson = JsonSerializer.Serialize(req.Tags ?? []);
            },
            e => new DownloadClientConfigDto(e.Id, e.Name, e.Implementation, e.SettingsJson,
                e.Priority, e.Enable, e.Category, e.RemovesCompletedDownloads, ParseInts(e.TagsJson)));

        // Notification config
        MapConfigCrud<NotificationConfig, NotificationConfigRequest, NotificationConfigDto, INotificationConfigRepository>(
            v1, "notification",
            (req, ct) => new NotificationConfig
            {
                Name = req.Name,
                Implementation = req.Implementation,
                SettingsJson = req.SettingsJson ?? "{}",
                Enable = req.Enable,
                SubscribedEventsJson = JsonSerializer.Serialize(req.SubscribedEvents ?? []),
                TagsJson = JsonSerializer.Serialize(req.Tags ?? []),
            },
            (existing, req) =>
            {
                existing.Name = req.Name;
                existing.Implementation = req.Implementation;
                existing.SettingsJson = req.SettingsJson ?? "{}";
                existing.Enable = req.Enable;
                existing.SubscribedEventsJson = JsonSerializer.Serialize(req.SubscribedEvents ?? []);
                existing.TagsJson = JsonSerializer.Serialize(req.Tags ?? []);
            },
            e => new NotificationConfigDto(e.Id, e.Name, e.Implementation, e.SettingsJson, e.Enable,
                ParseInts(e.SubscribedEventsJson), ParseInts(e.TagsJson)));

        // Discovery rules
        MapConfigCrud<DiscoveryRuleSet, DiscoveryRuleSetRequest, DiscoveryRuleSetDto, IDiscoveryRuleSetRepository>(
            v1, "discoveryrule",
            (req, ct) => new DiscoveryRuleSet
            {
                Name = req.Name,
                Enabled = req.Enabled,
                ConditionsJson = req.ConditionsJson ?? "[]",
                ActionJson = req.ActionJson ?? "{}",
            },
            (existing, req) =>
            {
                existing.Name = req.Name;
                existing.Enabled = req.Enabled;
                existing.ConditionsJson = req.ConditionsJson ?? "[]";
                existing.ActionJson = req.ActionJson ?? "{}";
            },
            e => new DiscoveryRuleSetDto(e.Id, e.Name, e.Enabled, e.ConditionsJson, e.ActionJson, e.LastRun));

        // Root folders (added in Phase 2; Phase 1 had only the entity)
        v1.MapGet("/rootfolder", async (IRootFolderRepository repo, CancellationToken ct) =>
            Results.Ok((await repo.ListAsync(ct)).Select(f =>
                new RootFolderDto(f.Id, f.Path, f.Accessible, f.FreeBytes, f.TotalBytes)).ToArray()));
        v1.MapPost("/rootfolder", async (RootFolderRequest req, IRootFolderRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Path))
            {
                return Results.BadRequest(new ApiErrorResponse([new ApiError("path", "Path is required")]));
            }
            var f = await repo.AddAsync(new RootFolder { Path = req.Path, Accessible = true }, ct);
            return Results.Created($"/api/v1/rootfolder/{f.Id}",
                new RootFolderDto(f.Id, f.Path, f.Accessible, f.FreeBytes, f.TotalBytes));
        });
        v1.MapDelete("/rootfolder/{id:int}", async (int id, IRootFolderRepository repo, CancellationToken ct) =>
        {
            await repo.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        // Application config endpoints (sliced into 3 façades)
        v1.MapGet("/config/host", async (IApplicationConfigRepository repo, CancellationToken ct) =>
        {
            var c = await repo.GetAsync(ct);
            return Results.Ok(new HostConfigDto(c.InstanceName, c.UrlBase, c.LogLevel));
        });
        v1.MapPut("/config/host", async (HostConfigRequest req, IApplicationConfigRepository repo, CancellationToken ct) =>
        {
            var c = await repo.GetAsync(ct);
            c.InstanceName = req.InstanceName;
            c.UrlBase = req.UrlBase;
            c.LogLevel = req.LogLevel;
            await repo.UpdateAsync(c, ct);
            return Results.Ok(new HostConfigDto(c.InstanceName, c.UrlBase, c.LogLevel));
        });

        v1.MapGet("/config/naming", async (IApplicationConfigRepository repo, CancellationToken ct) =>
        {
            var c = await repo.GetAsync(ct);
            return Results.Ok(new NamingConfigDto(c.ArtistFolderTemplate, c.FileTemplate));
        });
        v1.MapPut("/config/naming", async (NamingConfigDto req, IApplicationConfigRepository repo, CancellationToken ct) =>
        {
            var c = await repo.GetAsync(ct);
            c.ArtistFolderTemplate = req.ArtistFolderTemplate;
            c.FileTemplate = req.FileTemplate;
            await repo.UpdateAsync(c, ct);
            return Results.Ok(new NamingConfigDto(c.ArtistFolderTemplate, c.FileTemplate));
        });

        v1.MapGet("/config/mediamanagement", async (IApplicationConfigRepository repo, CancellationToken ct) =>
        {
            var c = await repo.GetAsync(ct);
            return Results.Ok(new MediaManagementConfigDto(c.FileOperation, c.ReplaceIllegalCharacters, c.IllegalCharacterReplacement));
        });
        v1.MapPut("/config/mediamanagement", async (MediaManagementConfigDto req, IApplicationConfigRepository repo, CancellationToken ct) =>
        {
            var c = await repo.GetAsync(ct);
            c.FileOperation = req.FileOperation;
            c.ReplaceIllegalCharacters = req.ReplaceIllegalCharacters;
            c.IllegalCharacterReplacement = req.IllegalCharacterReplacement;
            await repo.UpdateAsync(c, ct);
            return Results.Ok(new MediaManagementConfigDto(c.FileOperation, c.ReplaceIllegalCharacters, c.IllegalCharacterReplacement));
        });

        return app;
    }

    private static QualityProfileDto ToDto(QualityProfile p) =>
        new(p.Id, p.Name,
            ParseInts(p.AllowedQualityIdsJson),
            p.CutoffQualityId, p.UpgradeAllowed,
            p.MinFormatScore, p.MinSizeBytes, p.MaxSizeBytes,
            ParseInts(p.TagsJson));

    private static QualityProfile FromRequest(QualityProfileRequest req) => new()
    {
        Name = req.Name,
        AllowedQualityIdsJson = JsonSerializer.Serialize(req.AllowedQualityIds),
        CutoffQualityId = req.CutoffQualityId,
        UpgradeAllowed = req.UpgradeAllowed,
        MinFormatScore = req.MinFormatScore,
        MinSizeBytes = req.MinSizeBytes,
        MaxSizeBytes = req.MaxSizeBytes,
        TagsJson = JsonSerializer.Serialize(req.Tags ?? []),
    };

    private static int[] ParseInts(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void MapConfigCrud<TEntity, TRequest, TDto, TRepo>(
        RouteGroupBuilder group,
        string path,
        Func<TRequest, CancellationToken, TEntity> fromRequest,
        Action<TEntity, TRequest> applyRequest,
        Func<TEntity, TDto> toDto)
        where TEntity : class
        where TRequest : class
        where TDto : class
        where TRepo : class
    {
        group.MapGet($"/{path}", async (TRepo repo, CancellationToken ct) =>
        {
            var list = await CallListAsync<TEntity, TRepo>(repo, ct);
            return Results.Ok(list.Select(toDto).ToArray());
        });
        group.MapGet($"/{path}/{{id:int}}", async (int id, TRepo repo, CancellationToken ct) =>
        {
            var e = await CallGetAsync<TEntity, TRepo>(repo, id, ct);
            return e is null ? Results.NotFound() : Results.Ok(toDto(e));
        });
        group.MapPost($"/{path}", async (TRequest req, TRepo repo, CancellationToken ct) =>
        {
            var entity = fromRequest(req, ct);
            var created = await CallAddAsync<TEntity, TRepo>(repo, entity, ct);
            var dto = toDto(created);
            return Results.Created($"/api/v1/{path}", dto);
        });
        group.MapPut($"/{path}/{{id:int}}", async (int id, TRequest req, TRepo repo, CancellationToken ct) =>
        {
            var existing = await CallGetAsync<TEntity, TRepo>(repo, id, ct);
            if (existing is null) return Results.NotFound();
            applyRequest(existing, req);
            await CallUpdateAsync<TEntity, TRepo>(repo, existing, ct);
            return Results.Ok(toDto(existing));
        });
        group.MapDelete($"/{path}/{{id:int}}", async (int id, TRepo repo, CancellationToken ct) =>
        {
            await CallDeleteAsync<TRepo>(repo, id, ct);
            return Results.NoContent();
        });
    }

    private static Task<IReadOnlyList<TEntity>> CallListAsync<TEntity, TRepo>(TRepo repo, CancellationToken ct) =>
        (Task<IReadOnlyList<TEntity>>)typeof(TRepo).GetMethod("ListAsync")!.Invoke(repo, [ct])!;

    private static Task<TEntity?> CallGetAsync<TEntity, TRepo>(TRepo repo, int id, CancellationToken ct)
        where TEntity : class =>
        (Task<TEntity?>)typeof(TRepo).GetMethod("GetAsync")!.Invoke(repo, [id, ct])!;

    private static Task<TEntity> CallAddAsync<TEntity, TRepo>(TRepo repo, TEntity entity, CancellationToken ct) =>
        (Task<TEntity>)typeof(TRepo).GetMethod("AddAsync")!.Invoke(repo, [entity, ct])!;

    private static Task CallUpdateAsync<TEntity, TRepo>(TRepo repo, TEntity entity, CancellationToken ct) =>
        (Task)typeof(TRepo).GetMethod("UpdateAsync")!.Invoke(repo, [entity, ct])!;

    private static Task CallDeleteAsync<TRepo>(TRepo repo, int id, CancellationToken ct) =>
        (Task)typeof(TRepo).GetMethod("DeleteAsync")!.Invoke(repo, [id, ct])!;
}
