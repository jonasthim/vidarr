using Vidarr.Contracts.Models;

namespace Vidarr.Decision.CustomFormats;

public sealed record CustomFormatDefinition(int Id, string Name, IReadOnlyList<ICustomFormatSpec> Specs);

public sealed record CustomFormatMatch(int FormatId, string Name, int Score);

public sealed record CustomFormatScoring(
    IReadOnlyList<CustomFormatMatch> Matches,
    int TotalScore);

public sealed record ProfileFormatScoring(int FormatId, int Score);

public interface ICustomFormatEngine
{
    /// <summary>
    /// Evaluates every <paramref name="definitions"/> against the release context and
    /// returns the matched formats plus the profile-scored total (sum of
    /// <paramref name="profileScores"/>.Score over matched format ids).
    /// </summary>
    CustomFormatScoring Evaluate(
        CustomFormatSpecContext context,
        IEnumerable<CustomFormatDefinition> definitions,
        IReadOnlyDictionary<int, int> profileScores);
}

public sealed class CustomFormatEngine : ICustomFormatEngine
{
    public CustomFormatScoring Evaluate(
        CustomFormatSpecContext context,
        IEnumerable<CustomFormatDefinition> definitions,
        IReadOnlyDictionary<int, int> profileScores)
    {
        var matches = new List<CustomFormatMatch>();
        var total = 0;
        foreach (var def in definitions)
        {
            if (!FormatMatches(def, context)) continue;

            var score = profileScores.GetValueOrDefault(def.Id);
            matches.Add(new CustomFormatMatch(def.Id, def.Name, score));
            total += score;
        }
        return new CustomFormatScoring(matches, total);
    }

    /// <summary>
    /// Sonarr-style match rule: ALL Required specs must match. If any non-required
    /// specs exist, at least one of them must also match. If no specs exist on the
    /// format, it never matches anything (avoids accidental match-all).
    /// </summary>
    internal static bool FormatMatches(CustomFormatDefinition def, CustomFormatSpecContext ctx)
    {
        if (def.Specs.Count == 0)
        {
            return false;
        }

        var required = def.Specs.Where(s => s.Required).ToList();
        var optional = def.Specs.Where(s => !s.Required).ToList();

        if (required.Any(s => !s.Matches(ctx)))
        {
            return false;
        }

        if (optional.Count > 0 && !optional.Any(s => s.Matches(ctx)))
        {
            return false;
        }

        return true;
    }
}
