using Vidarr.Catalog.Seeding;

namespace Vidarr.Catalog.Tests;

public class DataSeederTests
{
    [Fact]
    public async Task Seed_creates_default_quality_profiles_and_application_config()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new DataSeeder();
            await sut.SeedAsync(db, default);

            db.QualityProfiles.Should().HaveCount(2);
            db.ApplicationConfigs.Should().HaveCount(1);
        }
        finally { conn.Dispose(); }
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        var (db, conn) = InMemoryDb.Create();
        try
        {
            var sut = new DataSeeder();
            await sut.SeedAsync(db, default);
            await sut.SeedAsync(db, default);
            await sut.SeedAsync(db, default);

            db.QualityProfiles.Should().HaveCount(2);
            db.ApplicationConfigs.Should().HaveCount(1);
        }
        finally { conn.Dispose(); }
    }
}
