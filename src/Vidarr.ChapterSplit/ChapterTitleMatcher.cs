using System.Text.RegularExpressions;

namespace Vidarr.ChapterSplit;

public sealed record ChapterMatchCandidate(int Id, string Title);

public sealed record ChapterMatch(int CandidateId, string CandidateTitle, double Score);

public interface IChapterTitleMatcher
{
    /// <summary>
    /// Returns the best assignment of chapters → candidates as a 1:1 mapping. Chapters
    /// whose best similarity falls below <see cref="MinimumScore"/> get a <c>null</c>
    /// match and surface for manual handling.
    /// </summary>
    IReadOnlyList<(MediaChapter Chapter, ChapterMatch? Match)> Assign(
        IEnumerable<MediaChapter> chapters,
        IEnumerable<ChapterMatchCandidate> candidates,
        string? artistNameContext = null);

    /// <summary>0..1 similarity score for one (chapter title, candidate title) pair.</summary>
    double Similarity(string chapterTitle, string candidateTitle, string? artistName = null);
}

public sealed partial class ChapterTitleMatcher : IChapterTitleMatcher
{
    public double MinimumScore { get; init; } = 0.4;

    public double Similarity(string chapterTitle, string candidateTitle, string? artistName = null)
    {
        if (string.IsNullOrWhiteSpace(chapterTitle) || string.IsNullOrWhiteSpace(candidateTitle))
        {
            return 0;
        }

        var chTokens = Tokenise(chapterTitle, artistName);
        var caTokens = Tokenise(candidateTitle, artistName);
        if (chTokens.Count == 0 || caTokens.Count == 0)
        {
            return 0;
        }

        // Token-set ratio = intersection / union, with the artist tokens stripped so that
        // "Daft Punk — Around the World" matches "Around the World" even when both carry the
        // artist tag explicitly.
        var intersection = chTokens.Intersect(caTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = chTokens.Union(caTokens, StringComparer.OrdinalIgnoreCase).Count();
        var ratio = union == 0 ? 0.0 : (double)intersection / union;

        // Artist-context bonus: if the chapter title raw text mentions the artist (we
        // stripped it from the token set), add a small positive bias because it adds
        // confidence to the otherwise lower-token intersection.
        if (!string.IsNullOrEmpty(artistName)
            && Normalise(chapterTitle).Contains(Normalise(artistName), StringComparison.Ordinal))
        {
            ratio = Math.Min(1.0, ratio + 0.1);
        }

        return ratio;
    }

    public IReadOnlyList<(MediaChapter Chapter, ChapterMatch? Match)> Assign(
        IEnumerable<MediaChapter> chapters,
        IEnumerable<ChapterMatchCandidate> candidates,
        string? artistNameContext = null)
    {
        var chapterList = chapters.ToList();
        var candidateList = candidates.ToList();
        var used = new HashSet<int>();
        var result = new List<(MediaChapter, ChapterMatch?)>();

        foreach (var chapter in chapterList)
        {
            ChapterMatch? best = null;
            foreach (var c in candidateList)
            {
                if (used.Contains(c.Id)) continue;
                var score = Similarity(chapter.Title ?? string.Empty, c.Title, artistNameContext);
                if (score < MinimumScore) continue;
                if (best is null || score > best.Score)
                {
                    best = new ChapterMatch(c.Id, c.Title, score);
                }
            }
            if (best is not null)
            {
                used.Add(best.CandidateId);
            }
            result.Add((chapter, best));
        }
        return result;
    }

    private static List<string> Tokenise(string raw, string? artistName)
    {
        var normalised = Normalise(raw);
        if (!string.IsNullOrEmpty(artistName))
        {
            normalised = normalised.Replace(Normalise(artistName), " ", StringComparison.Ordinal);
        }
        return [.. NonWordRegex().Split(normalised)
            .Where(t => !string.IsNullOrEmpty(t) && t.Length > 1)];
    }

    private static string Normalise(string raw) =>
        WhitespaceRegex().Replace(raw.ToLowerInvariant().Trim(), " ");

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
