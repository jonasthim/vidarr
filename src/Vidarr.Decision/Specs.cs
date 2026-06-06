using Vidarr.Contracts.Models;

namespace Vidarr.Decision;

public interface IReleaseSpec
{
    string Name { get; }
    bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason);
}

public sealed record DecisionContext(
    int? ArtistId,
    int? MusicVideoId,
    Quality? CurrentBestQuality,
    QualityProfile Profile,
    IReadOnlySet<string> BlocklistedReleaseTitles,
    int CurrentFormatScore = 0,
    IReadOnlySet<int>? MatchedFormatIds = null,
    IReadOnlySet<int>? RequiredFormatIds = null)
{
    public IReadOnlySet<int> MatchedFormatIds { get; init; } = MatchedFormatIds ?? new HashSet<int>();
    public IReadOnlySet<int> RequiredFormatIds { get; init; } = RequiredFormatIds ?? new HashSet<int>();
}

public sealed record QualityProfile(
    IReadOnlyList<Quality> AllowedQualities,
    Quality Cutoff,
    bool UpgradeAllowed,
    long? MinSizeBytes = null,
    long? MaxSizeBytes = null,
    int MinFormatScore = 0);

public sealed class AlreadyImportedSpec : IReleaseSpec
{
    public string Name => nameof(AlreadyImportedSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (ctx.CurrentBestQuality is not null && !ctx.Profile.UpgradeAllowed)
        {
            reason = "Already imported and upgrades disallowed";
            return false;
        }
        reason = null;
        return true;
    }
}

public sealed class BlocklistedSpec : IReleaseSpec
{
    public string Name => nameof(BlocklistedSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (ctx.BlocklistedReleaseTitles.Contains(release.Title))
        {
            reason = $"Release '{release.Title}' is blocklisted";
            return false;
        }
        reason = null;
        return true;
    }
}

public sealed class QualityAllowedSpec : IReleaseSpec
{
    public string Name => nameof(QualityAllowedSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (!ctx.Profile.AllowedQualities.Any(q => q.Id == parsed.Quality.Id))
        {
            reason = $"Quality {parsed.Quality.Name} not in profile";
            return false;
        }
        reason = null;
        return true;
    }
}

public sealed class MinSizeSpec : IReleaseSpec
{
    public string Name => nameof(MinSizeSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (ctx.Profile.MinSizeBytes is { } min && release.SizeBytes is { } size && size < min)
        {
            reason = $"Release size {size} below minimum {min}";
            return false;
        }
        reason = null;
        return true;
    }
}

public sealed class MaxSizeSpec : IReleaseSpec
{
    public string Name => nameof(MaxSizeSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (ctx.Profile.MaxSizeBytes is { } max && release.SizeBytes is { } size && size > max)
        {
            reason = $"Release size {size} exceeds maximum {max}";
            return false;
        }
        reason = null;
        return true;
    }
}

public sealed class UpgradeAllowedSpec : IReleaseSpec
{
    public string Name => nameof(UpgradeAllowedSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (ctx.CurrentBestQuality is { } current
            && QualityRank(current, ctx.Profile) >= QualityRank(parsed.Quality, ctx.Profile))
        {
            reason = $"Existing {current.Name} is not worse than candidate {parsed.Quality.Name}";
            return false;
        }
        reason = null;
        return true;
    }

    internal static int QualityRank(Quality quality, QualityProfile profile)
    {
        for (var i = 0; i < profile.AllowedQualities.Count; i++)
        {
            if (profile.AllowedQualities[i].Id == quality.Id)
            {
                return i;
            }
        }
        return -1;
    }
}

/// <summary>
/// Rejects when the profile mandates a minimum Custom-Format score and the release
/// scored below it. Score must be precomputed by the CustomFormatEngine and passed
/// in via <see cref="RemoteRelease.Score"/>.
/// </summary>
public sealed class MinFormatScoreSpec : IReleaseSpec
{
    public string Name => nameof(MinFormatScoreSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        // No threshold → always passes. Score < 0 still allowed if MinFormatScore is 0 or negative.
        if (ctx.Profile.MinFormatScore == 0)
        {
            reason = null;
            return true;
        }
        if (ctx.CurrentFormatScore < ctx.Profile.MinFormatScore)
        {
            reason = $"Custom-format score {ctx.CurrentFormatScore} < minimum {ctx.Profile.MinFormatScore}";
            return false;
        }
        reason = null;
        return true;
    }
}

/// <summary>
/// Rejects when at least one of the provided Custom-Format ids is marked
/// "required" by the profile (i.e. has a score of -infinity stand-in in the
/// profile-scores dictionary, expressed here as <see cref="DecisionContext.RequiredFormatIds"/>)
/// and that format did not score.
/// </summary>
public sealed class CustomFormatRequiredSpec : IReleaseSpec
{
    public string Name => nameof(CustomFormatRequiredSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (ctx.RequiredFormatIds.Count == 0)
        {
            reason = null;
            return true;
        }
        var missing = ctx.RequiredFormatIds.Where(id => !ctx.MatchedFormatIds.Contains(id)).ToList();
        if (missing.Count > 0)
        {
            reason = $"Required custom formats missing: {string.Join(", ", missing)}";
            return false;
        }
        reason = null;
        return true;
    }
}

public sealed class CutoffMetSpec : IReleaseSpec
{
    public string Name => nameof(CutoffMetSpec);

    public bool IsSatisfied(ReleaseInfo release, ParsedReleaseInfo parsed, DecisionContext ctx, out string? reason)
    {
        if (ctx.CurrentBestQuality is { } current
            && UpgradeAllowedSpec.QualityRank(current, ctx.Profile) >= UpgradeAllowedSpec.QualityRank(ctx.Profile.Cutoff, ctx.Profile))
        {
            reason = $"Cutoff {ctx.Profile.Cutoff.Name} already met by {current.Name}";
            return false;
        }
        reason = null;
        return true;
    }
}
