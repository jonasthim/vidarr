using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vidarr.Catalog.Entities;
using Vidarr.Contracts.Models;

namespace Vidarr.Catalog.Seeding;

public interface IDataSeeder
{
    Task SeedAsync(VidarrDbContext db, CancellationToken ct);
}

public sealed class DataSeeder : IDataSeeder
{
    public async Task SeedAsync(VidarrDbContext db, CancellationToken ct)
    {
        await SeedDefaultQualityProfileAsync(db, ct);
        await SeedApplicationConfigAsync(db, ct);
    }

    private static async Task SeedDefaultQualityProfileAsync(VidarrDbContext db, CancellationToken ct)
    {
        if (await db.QualityProfiles.AnyAsync(ct))
        {
            return;
        }

        var allowed = new[]
        {
            Quality.Webdl480p.Id,
            Quality.Webdl720p.Id,
            Quality.Hdtv720p.Id,
            Quality.Bluray720p.Id,
            Quality.Webdl1080p.Id,
            Quality.Hdtv1080p.Id,
            Quality.Bluray1080p.Id,
            Quality.Webdl2160p.Id,
            Quality.Bluray2160p.Id,
        };
        db.QualityProfiles.Add(new QualityProfile
        {
            Name = "Any",
            AllowedQualityIdsJson = JsonSerializer.Serialize(allowed),
            CutoffQualityId = Quality.Webdl1080p.Id,
            UpgradeAllowed = true,
        });
        db.QualityProfiles.Add(new QualityProfile
        {
            Name = "HD-1080p",
            AllowedQualityIdsJson = JsonSerializer.Serialize(new[]
            {
                Quality.Webdl720p.Id, Quality.Hdtv720p.Id, Quality.Bluray720p.Id,
                Quality.Webdl1080p.Id, Quality.Hdtv1080p.Id, Quality.Bluray1080p.Id,
            }),
            CutoffQualityId = Quality.Webdl1080p.Id,
            UpgradeAllowed = true,
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedApplicationConfigAsync(VidarrDbContext db, CancellationToken ct)
    {
        if (await db.ApplicationConfigs.AnyAsync(ct))
        {
            return;
        }

        db.ApplicationConfigs.Add(new ApplicationConfig
        {
            InstanceName = "Vidarr",
            Updated = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
