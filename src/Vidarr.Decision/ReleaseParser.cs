using System.Globalization;
using System.Text.RegularExpressions;
using Vidarr.Contracts.Models;

namespace Vidarr.Decision;

public sealed partial class ReleaseParser : IReleaseParser
{
    private static readonly string[] Tags = ["PROPER", "REPACK", "REAL", "INTERNAL"];

    public ParsedReleaseInfo Parse(string releaseTitle)
    {
        if (string.IsNullOrWhiteSpace(releaseTitle))
        {
            return new ParsedReleaseInfo(null, string.Empty, null, Quality.Unknown, null, []);
        }

        var normalised = NormaliseSeparators(releaseTitle);
        var quality = DetectQuality(normalised);
        var releaseGroup = DetectReleaseGroup(releaseTitle.Trim());
        var year = DetectYear(normalised);
        var tags = DetectTags(normalised);

        var (artist, title) = SplitArtistTitle(normalised, year, quality, releaseGroup);

        return new ParsedReleaseInfo(artist, title, year, quality, releaseGroup, tags);
    }

    private static string NormaliseSeparators(string raw)
    {
        var trimmed = raw.Trim();

        // If the string is heavily dotted (scene-style), turn dots into spaces — except where
        // they're separating known quality tokens (we restore those in DetectQuality directly).
        var dotCount = trimmed.Count(c => c == '.');
        var spaceCount = trimmed.Count(c => c == ' ');
        if (dotCount > spaceCount + 2)
        {
            trimmed = trimmed.Replace('.', ' ');
        }

        return WhitespaceRegex().Replace(trimmed, " ").Trim();
    }

    private static Quality DetectQuality(string normalised)
    {
        var match = QualityRegex().Match(normalised);
        if (!match.Success)
        {
            return Quality.Unknown;
        }

        var sourceText = match.Groups["source"].Value.ToUpperInvariant().Replace("-", string.Empty).Replace(".", string.Empty).Replace(" ", string.Empty);
        var resolutionText = match.Groups["res"].Value.ToUpperInvariant();

        var source = sourceText switch
        {
            "WEBDL" or "WEBRIP" => Source.Webdl,
            "BLURAY" or "BDRIP" or "BD" => Source.Bluray,
            "HDTV" => Source.Hdtv,
            "DVD" or "DVDRIP" => Source.Dvd,
            _ => Source.Unknown,
        };

        var resolution = resolutionText switch
        {
            "480P" => Resolution.R480p,
            "720P" => Resolution.R720p,
            "1080P" => Resolution.R1080p,
            "2160P" or "4K" => Resolution.R2160p,
            _ => Resolution.Unknown,
        };

        if (source == Source.Dvd && resolution == Resolution.Unknown)
        {
            resolution = Resolution.R480p;
        }

        return Quality.All.FirstOrDefault(q => q.Source == source && q.Resolution == resolution) ?? Quality.Unknown;
    }

    private static int? DetectYear(string normalised)
    {
        // First try parenthesised / bracketed year — most reliable.
        var paren = ParenYearRegex().Match(normalised);
        if (paren.Success && int.TryParse(paren.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var py)
            && py is >= 1900 and <= 2100)
        {
            return py;
        }

        foreach (Match m in YearRegex().Matches(normalised))
        {
            if (int.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) && year is >= 1900 and <= 2100)
            {
                return year;
            }
        }
        return null;
    }

    private static string? DetectReleaseGroup(string raw)
    {
        var bracket = BracketGroupRegex().Match(raw);
        if (bracket.Success)
        {
            return bracket.Groups[1].Value;
        }

        var dash = DashGroupRegex().Match(raw);
        if (dash.Success)
        {
            return dash.Groups[1].Value;
        }

        var trailing = TrailingGroupRegex().Match(raw);
        if (trailing.Success)
        {
            return trailing.Groups[1].Value;
        }

        return null;
    }

    private static List<string> DetectTags(string normalised)
    {
        var found = new List<string>();
        foreach (var tag in Tags)
        {
            if (Regex.IsMatch(normalised, $@"\b{tag}\b", RegexOptions.IgnoreCase))
            {
                found.Add(tag);
            }
        }
        return found;
    }

    private static (string? Artist, string Title) SplitArtistTitle(string normalised, int? year, Quality quality, string? releaseGroup)
    {
        var working = normalised;

        // Strip release group fragments
        if (releaseGroup is not null)
        {
            working = Regex.Replace(working, $@"\s*[\[\(]?\b{Regex.Escape(releaseGroup)}\b[\]\)]?\s*$", string.Empty, RegexOptions.IgnoreCase);
            working = Regex.Replace(working, $@"\s*-\s*{Regex.Escape(releaseGroup)}\s*$", string.Empty, RegexOptions.IgnoreCase);
        }

        // Strip year fragments
        if (year is not null)
        {
            working = Regex.Replace(working, $@"\s*[\[\(]?\b{year}\b[\]\)]?\s*", " ");
        }

        // Strip quality fragments
        if (quality != Quality.Unknown)
        {
            working = QualityRegex().Replace(working, " ");
        }

        // Strip known tags
        foreach (var tag in Tags)
        {
            working = Regex.Replace(working, $@"\b{tag}\b", string.Empty, RegexOptions.IgnoreCase);
        }

        // Strip generic bracketed annotations e.g. "[Official Music Video]"
        working = BracketAnnotationRegex().Replace(working, " ");

        // Strip stray codecs not part of quality regex
        working = CodecRegex().Replace(working, " ");

        working = WhitespaceRegex().Replace(working, " ").Trim();

        // Split on the first " - "
        var idx = working.IndexOf(" - ", StringComparison.Ordinal);
        if (idx > 0 && idx < working.Length - 3)
        {
            var artist = working[..idx].Trim();
            var title = working[(idx + 3)..].Trim();
            return (string.IsNullOrEmpty(artist) ? null : artist, title);
        }

        return (null, working);
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\b(?<source>WEB[\s\.\-]?DL|WEBRip|BluRay|BDRip|BD|HDTV|DVD(?:Rip)?)\b[\s\.\-]*(?<res>480p|720p|1080p|2160p|4K)?|\b(?<res>480p|720p|1080p|2160p|4K)\b[\s\.\-]*\b(?<source>WEB[\s\.\-]?DL|WEBRip|BluRay|BDRip|BD|HDTV|DVD(?:Rip)?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex QualityRegex();

    [GeneratedRegex(@"[\(\[](\d{4})[\)\]]", RegexOptions.Compiled)]
    private static partial Regex ParenYearRegex();

    [GeneratedRegex(@"\b(19|20)\d{2}\b", RegexOptions.Compiled)]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\[([A-Z0-9][A-Z0-9_\.\-]{1,})\]\s*$", RegexOptions.Compiled)]
    private static partial Regex BracketGroupRegex();

    [GeneratedRegex(@"-([A-Z][A-Z0-9_]{1,})\s*$", RegexOptions.Compiled)]
    private static partial Regex DashGroupRegex();

    [GeneratedRegex(@"\s([A-Z][A-Z0-9]{3,})\s*$", RegexOptions.Compiled)]
    private static partial Regex TrailingGroupRegex();

    [GeneratedRegex(@"\[(?:[A-Za-z][\w\s\-]+?)\]", RegexOptions.Compiled)]
    private static partial Regex BracketAnnotationRegex();

    [GeneratedRegex(@"\bH[\.\s]?2?64\b|\bH[\.\s]?2?65\b|\bx264\b|\bx265\b|\bHEVC\b|\bAAC\b|\bAC3\b|\bOpus\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex CodecRegex();
}
