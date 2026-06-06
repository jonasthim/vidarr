using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vidarr.Catalog;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Models;
using Vidarr.Rules;
using Vidarr.Tests.Common;

namespace Vidarr.Rules.Tests;

public class DiscoveryRuleEngineTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly VidarrDbContext _db;
    private readonly DiscoveryRuleEngine _sut;

    public DiscoveryRuleEngineTests()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var opts = new DbContextOptionsBuilder<VidarrDbContext>().UseSqlite(_conn).Options;
        _db = new VidarrDbContext(opts);
        _db.Database.EnsureCreated();
        _sut = new DiscoveryRuleEngine(
            new DiscoveryRuleSetRepository(_db),
            new ArtistRepository(_db),
            new MusicVideoRepository(_db),
            new FakeClock(),
            NullLogger<DiscoveryRuleEngine>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Artist> SeedArtistAsync(string name, string? country = "US", string[]? genres = null)
    {
        var a = new Artist
        {
            Name = name,
            SortName = name,
            Country = country,
            GenresJson = JsonSerializer.Serialize(genres ?? []),
            ExternalIdsJson = "{}",
            YouTubeChannelIdsJson = "[]",
            AliasesJson = "[]",
            ImagesJson = "[]",
        };
        _db.Artists.Add(a);
        await _db.SaveChangesAsync();
        return a;
    }

    private async Task SeedVideoAsync(Artist artist, string title, int? year,
        MusicVideoType type = MusicVideoType.Official, string[]? genres = null, bool monitored = false)
    {
        _db.MusicVideos.Add(new MusicVideo
        {
            ArtistId = artist.Id,
            Title = title,
            Year = year,
            Type = type,
            Monitored = monitored,
            GenresJson = JsonSerializer.Serialize(genres ?? []),
        });
        await _db.SaveChangesAsync();
    }

    private int _ruleSeq;
    private async Task<DiscoveryRuleSet> SeedRuleAsync(string conditionsJson, string actionJson, bool enabled = true)
    {
        var rule = new DiscoveryRuleSet
        {
            Name = $"test-rule-{++_ruleSeq}",
            Enabled = enabled,
            ConditionsJson = conditionsJson,
            ActionJson = actionJson,
        };
        _db.DiscoveryRuleSets.Add(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    [Fact]
    public async Task Rule_matches_and_marks_videos_monitored()
    {
        var daft = await SeedArtistAsync("Daft Punk", country: "FR", genres: ["Electronic", "Synthwave"]);
        await SeedVideoAsync(daft, "Around the World", 1997);
        await SeedVideoAsync(daft, "Da Funk", 1995);

        await SeedRuleAsync(
            conditionsJson: """[{"type":"GenreIn","values":["Synthwave"]}]""",
            actionJson: """{"monitorMode":"All"}""");

        var results = await _sut.EvaluateAllAsync(default);
        results.Should().ContainSingle().Which.VideosMonitored.Should().Be(2);

        (await new MusicVideoRepository(_db).ListByArtistAsync(daft.Id, default))
            .All(v => v.Monitored).Should().BeTrue();
    }

    [Fact]
    public async Task Rule_skips_already_monitored_videos()
    {
        var artist = await SeedArtistAsync("X", genres: ["Synthwave"]);
        await SeedVideoAsync(artist, "Old", 2020, monitored: true);
        await SeedVideoAsync(artist, "New", 2024, monitored: false);

        await SeedRuleAsync(
            """[{"type":"GenreIn","values":["Synthwave"]}]""",
            """{"monitorMode":"All"}""");

        var result = (await _sut.EvaluateAllAsync(default))[0];
        result.VideosMonitored.Should().Be(1); // only the new one
    }

    [Fact]
    public async Task Rule_applies_artist_action_only_when_target_field_unset()
    {
        var artist = await SeedArtistAsync("X", genres: ["Synthwave"]);
        artist.QualityProfileId = 99;
        await _db.SaveChangesAsync();
        await SeedVideoAsync(artist, "v1", 2024);

        await SeedRuleAsync(
            """[{"type":"GenreIn","values":["Synthwave"]}]""",
            """{"qualityProfileId":3,"rootFolderPath":"/auto","monitorMode":"NewOnly"}""");

        await _sut.EvaluateAllAsync(default);
        var refreshed = (await new ArtistRepository(_db).GetAsync(artist.Id, default))!;
        refreshed.QualityProfileId.Should().Be(99); // preserved (already set)
        refreshed.RootFolderPath.Should().Be("/auto"); // adopted (was unset)
        refreshed.MonitorMode.Should().Be(MonitorMode.NewOnly);
        refreshed.Monitored.Should().BeTrue();
    }

    [Fact]
    public async Task Disabled_rules_are_skipped()
    {
        var artist = await SeedArtistAsync("X", genres: ["Synthwave"]);
        await SeedVideoAsync(artist, "v1", 2024);
        await SeedRuleAsync(
            """[{"type":"GenreIn","values":["Synthwave"]}]""",
            """{"monitorMode":"All"}""",
            enabled: false);

        var results = await _sut.EvaluateAllAsync(default);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Rule_with_no_matches_runs_but_returns_zero()
    {
        await SeedArtistAsync("X", genres: ["Pop"]);
        await SeedRuleAsync(
            """[{"type":"GenreIn","values":["Synthwave"]}]""",
            """{"monitorMode":"All"}""");

        var result = (await _sut.EvaluateAllAsync(default))[0];
        result.VideosMonitored.Should().Be(0);
        result.Matched.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAsync_returns_null_when_rule_missing()
    {
        var result = await _sut.EvaluateAsync(9999, default);
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_runs_specified_rule_only()
    {
        var artist = await SeedArtistAsync("X", genres: ["Synthwave"]);
        await SeedVideoAsync(artist, "v1", 2024);
        var ruleA = await SeedRuleAsync(
            """[{"type":"GenreIn","values":["Synthwave"]}]""",
            """{"monitorMode":"All"}""");
        await SeedRuleAsync(
            """[{"type":"GenreIn","values":["Jazz"]}]""",
            """{"monitorMode":"All"}""");

        var result = await _sut.EvaluateAsync(ruleA.Id, default);
        result.Should().NotBeNull();
        result!.VideosMonitored.Should().Be(1);
    }

    [Fact]
    public async Task Video_genres_take_precedence_over_artist_genres()
    {
        var artist = await SeedArtistAsync("X", genres: ["Pop"]);
        await SeedVideoAsync(artist, "synth-video", 2024, genres: ["Synthwave"]);
        await SeedVideoAsync(artist, "pop-video", 2024, genres: ["Pop"]);

        await SeedRuleAsync(
            """[{"type":"GenreIn","values":["Synthwave"]}]""",
            """{"monitorMode":"All"}""");

        var result = (await _sut.EvaluateAllAsync(default))[0];
        result.VideosMonitored.Should().Be(1);
    }
}
