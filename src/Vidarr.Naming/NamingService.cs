using System.Text;
using System.Text.RegularExpressions;
using Vidarr.Contracts.Models;

namespace Vidarr.Naming;

public sealed partial class NamingService : INamingService
{
    public string BuildRelativePath(NamingInput input, NamingConfig config)
    {
        var tokens = BuildTokens(input);
        var artistFolder = Render(config.ArtistFolderTemplate, tokens, config);
        var fileBase = Render(config.FileTemplate, tokens, config);
        var extension = NormalizeExtension(input.Extension);

        return Path.Combine(artistFolder, fileBase + extension);
    }

    private static Dictionary<string, string> BuildTokens(NamingInput input)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Artist Name"] = input.ArtistName,
            ["Title"] = input.Title,
            ["Year"] = input.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ["Quality Full"] = input.Quality.Name,
        };

        if (input.ExtraTokens is not null)
        {
            foreach (var (k, v) in input.ExtraTokens)
            {
                tokens[k] = v;
            }
        }

        return tokens;
    }

    private static string Render(string template, Dictionary<string, string> tokens, NamingConfig config)
    {
        var withTokens = TokenRegex().Replace(template, match =>
        {
            var name = match.Groups[1].Value.Trim();
            return tokens.TryGetValue(name, out var value) ? value : string.Empty;
        });

        var collapsed = ParenGapRegex().Replace(withTokens, string.Empty);
        var sanitized = config.ReplaceIllegalCharacters
            ? ReplaceIllegalChars(collapsed, config.IllegalCharacterReplacement)
            : collapsed;

        return CollapseWhitespace(sanitized).Trim();
    }

    private static string ReplaceIllegalChars(string value, char replacement)
    {
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars())
        {
            '/',
            '\\',
        };
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(invalid.Contains(c) ? replacement : c);
        }
        return sb.ToString();
    }

    private static string CollapseWhitespace(string value) => WhitespaceRegex().Replace(value, " ");

    private static string NormalizeExtension(string ext)
    {
        if (string.IsNullOrEmpty(ext))
        {
            return string.Empty;
        }
        var trimmed = ext.TrimStart('.');
        return string.IsNullOrEmpty(trimmed) ? string.Empty : "." + trimmed;
    }

    [GeneratedRegex(@"\{([^{}]+)\}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    // Removes empty bracket/paren groups left behind by missing tokens, e.g. "()", "[]".
    [GeneratedRegex(@"\s*\(\s*\)|\s*\[\s*\]", RegexOptions.Compiled)]
    private static partial Regex ParenGapRegex();
}
