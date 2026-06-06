using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vidarr.Catalog.Entities;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;

namespace Vidarr.Rules;

public sealed record DiscoveryEvaluationResult(int RuleId, string RuleName, int Matched, int VideosMonitored);

public interface IDiscoveryRuleEngine
{
    Task<IReadOnlyList<DiscoveryEvaluationResult>> EvaluateAllAsync(CancellationToken ct);
    Task<DiscoveryEvaluationResult?> EvaluateAsync(int ruleId, CancellationToken ct);
}

public sealed class DiscoveryRuleEngine : IDiscoveryRuleEngine
{
    private readonly IDiscoveryRuleSetRepository _ruleRepo;
    private readonly IArtistRepository _artists;
    private readonly IMusicVideoRepository _videos;
    private readonly ISystemClock _clock;
    private readonly ILogger<DiscoveryRuleEngine> _logger;

    public DiscoveryRuleEngine(
        IDiscoveryRuleSetRepository ruleRepo,
        IArtistRepository artists,
        IMusicVideoRepository videos,
        ISystemClock clock,
        ILogger<DiscoveryRuleEngine> logger)
    {
        _ruleRepo = ruleRepo;
        _artists = artists;
        _videos = videos;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveryEvaluationResult>> EvaluateAllAsync(CancellationToken ct)
    {
        var rules = await _ruleRepo.ListAsync(ct);
        var enabled = rules.Where(r => r.Enabled).ToList();
        var results = new List<DiscoveryEvaluationResult>();
        foreach (var rule in enabled)
        {
            var result = await EvaluateRuleAsync(rule, ct);
            results.Add(result);
        }
        return results;
    }

    public async Task<DiscoveryEvaluationResult?> EvaluateAsync(int ruleId, CancellationToken ct)
    {
        var rule = await _ruleRepo.GetAsync(ruleId, ct);
        if (rule is null)
        {
            return null;
        }
        return await EvaluateRuleAsync(rule, ct);
    }

    private async Task<DiscoveryEvaluationResult> EvaluateRuleAsync(DiscoveryRuleSet rule, CancellationToken ct)
    {
        var conditions = DiscoveryConditionParser.Parse(rule.ConditionsJson);
        var action = DiscoveryActionParser.Parse(rule.ActionJson);

        var artists = await _artists.ListAsync(ct);
        var matchedVideos = 0;
        var videosMonitored = 0;
        var artistsTouched = new HashSet<int>();

        foreach (var artist in artists)
        {
            var artistGenres = SafeListString(artist.GenresJson);
            var artistCountry = artist.Country;
            var artistVideos = await _videos.ListByArtistAsync(artist.Id, ct);
            foreach (var video in artistVideos.Where(v => !v.Monitored))
            {
                ct.ThrowIfCancellationRequested();
                var videoGenres = SafeListString(video.GenresJson);
                var effectiveGenres = videoGenres.Count > 0 ? videoGenres : artistGenres;

                var ctx = new DiscoveryContext(
                    Year: video.Year,
                    Genres: effectiveGenres,
                    Country: artistCountry,
                    Type: video.Type);
                if (!conditions.All(c => c.Matches(ctx))) continue;

                matchedVideos++;
                video.Monitored = true;
                await _videos.UpdateAsync(video, ct);
                videosMonitored++;

                if (artistsTouched.Add(artist.Id))
                {
                    ApplyActionToArtist(artist, action);
                    await _artists.UpdateAsync(artist, ct);
                }
            }
        }

        rule.LastRun = _clock.UtcNow;
        await _ruleRepo.UpdateAsync(rule, ct);

        _logger.LogInformation("DiscoveryRule {Name}: {Matched} matched, {Monitored} videos monitored",
            rule.Name, matchedVideos, videosMonitored);
        return new DiscoveryEvaluationResult(rule.Id, rule.Name, matchedVideos, videosMonitored);
    }

    private static void ApplyActionToArtist(Artist artist, DiscoveryAction action)
    {
        if (action.QualityProfileId is { } qpId && artist.QualityProfileId == 0)
        {
            artist.QualityProfileId = qpId;
        }
        if (!string.IsNullOrEmpty(action.RootFolderPath) && string.IsNullOrEmpty(artist.RootFolderPath))
        {
            artist.RootFolderPath = action.RootFolderPath;
        }
        if (action.MonitorMode is { } mm)
        {
            artist.MonitorMode = mm;
            artist.Monitored = mm != MonitorMode.None;
        }
        // Tag merging is left for a future Artist.TagsJson migration; the rule action
        // is persisted so a later phase can backfill without losing intent.
    }

    private static List<string> SafeListString(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
