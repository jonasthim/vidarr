using System.Text.Json;
using System.Text.RegularExpressions;
using Vidarr.Contracts.Models;

namespace Vidarr.Decision.CustomFormats;

/// <summary>
/// Inputs a CustomFormatSpec sees. We pass parsed-info AND raw release info because
/// some specs care about release-title regex (raw) while others key off parsed fields.
/// </summary>
public sealed record CustomFormatSpecContext(
    ReleaseInfo Release,
    ParsedReleaseInfo Parsed,
    IReadOnlyDictionary<string, string> IndexerFlags);

public interface ICustomFormatSpec
{
    string Implementation { get; }
    bool Negate { get; }
    bool Required { get; }
    bool Matches(CustomFormatSpecContext ctx);
}

internal abstract class CustomFormatSpecBase : ICustomFormatSpec
{
    public abstract string Implementation { get; }
    public bool Negate { get; set; }
    public bool Required { get; set; }
    public bool Matches(CustomFormatSpecContext ctx)
    {
        var raw = EvaluateRaw(ctx);
        return Negate ? !raw : raw;
    }
    protected abstract bool EvaluateRaw(CustomFormatSpecContext ctx);
}

internal sealed class ReleaseTitleSpec : CustomFormatSpecBase
{
    public override string Implementation => "ReleaseTitleSpecification";
    public string Pattern { get; set; } = string.Empty;
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx)
    {
        if (string.IsNullOrEmpty(Pattern)) return false;
        try
        {
            return Regex.IsMatch(ctx.Release.Title, Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
        }
        catch (RegexMatchTimeoutException) { return false; }
        catch (ArgumentException) { return false; }
    }
}

internal sealed class ReleaseGroupSpec : CustomFormatSpecBase
{
    public override string Implementation => "ReleaseGroupSpecification";
    public string Pattern { get; set; } = string.Empty;
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx)
    {
        if (string.IsNullOrEmpty(Pattern) || string.IsNullOrEmpty(ctx.Parsed.ReleaseGroup)) return false;
        try
        {
            return Regex.IsMatch(ctx.Parsed.ReleaseGroup, Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(50));
        }
        catch (RegexMatchTimeoutException) { return false; }
        catch (ArgumentException) { return false; }
    }
}

internal sealed class IndexerFlagSpec : CustomFormatSpecBase
{
    public override string Implementation => "IndexerFlagSpecification";
    public string FlagKey { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx)
    {
        if (string.IsNullOrEmpty(FlagKey)) return false;
        if (!ctx.IndexerFlags.TryGetValue(FlagKey, out var actual)) return false;
        return ExpectedValue is null
            ? !string.IsNullOrEmpty(actual)
            : string.Equals(actual, ExpectedValue, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class SourceSpec : CustomFormatSpecBase
{
    public override string Implementation => "SourceSpecification";
    public Source ExpectedSource { get; set; }
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx) =>
        ctx.Parsed.Quality.Source == ExpectedSource;
}

internal sealed class ResolutionSpec : CustomFormatSpecBase
{
    public override string Implementation => "ResolutionSpecification";
    public Resolution ExpectedResolution { get; set; }
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx) =>
        ctx.Parsed.Quality.Resolution == ExpectedResolution;
}

internal sealed class LanguageSpec : CustomFormatSpecBase
{
    public override string Implementation => "LanguageSpecification";
    /// <summary>Language tag matched against parsed tags or release extras["language"].</summary>
    public string LanguageTag { get; set; } = string.Empty;
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx)
    {
        if (string.IsNullOrEmpty(LanguageTag)) return false;
        if (ctx.Parsed.Tags.Any(t => string.Equals(t, LanguageTag, StringComparison.OrdinalIgnoreCase))) return true;
        return ctx.Release.ExtraMetadata.TryGetValue("language", out var lang)
            && string.Equals(lang, LanguageTag, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class SizeSpec : CustomFormatSpecBase
{
    public override string Implementation => "SizeSpecification";
    public long? MinBytes { get; set; }
    public long? MaxBytes { get; set; }
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx)
    {
        if (ctx.Release.SizeBytes is null) return false;
        var size = ctx.Release.SizeBytes.Value;
        if (MinBytes is { } min && size < min) return false;
        if (MaxBytes is { } max && size > max) return false;
        return true;
    }
}

internal sealed class YouTubeChannelSpec : CustomFormatSpecBase
{
    public override string Implementation => "YouTubeChannelSpecification";
    public string ChannelMatch { get; set; } = string.Empty;
    protected override bool EvaluateRaw(CustomFormatSpecContext ctx)
    {
        if (string.IsNullOrEmpty(ChannelMatch)) return false;

        if (ctx.Release.ExtraMetadata.TryGetValue("channelId", out var channelId)
            && string.Equals(channelId, ChannelMatch, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (ctx.Release.ExtraMetadata.TryGetValue("channelTitle", out var channelTitle)
            && channelTitle.Contains(ChannelMatch, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }
}

/// <summary>
/// Materialises typed <see cref="ICustomFormatSpec"/> instances from the JSON shape
/// the UI sends and the database stores:
/// <c>[ { implementation, negate, required, fields: { ... } } ]</c>.
/// </summary>
public static class CustomFormatSpecParser
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<ICustomFormatSpec> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return [];
        }
        List<RawSpec>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<RawSpec>>(json, JsonOpts);
        }
        catch (JsonException)
        {
            return [];
        }
        if (raw is null || raw.Count == 0) return [];

        var result = new List<ICustomFormatSpec>();
        foreach (var r in raw)
        {
            if (string.IsNullOrEmpty(r.Implementation)) continue;
            var instance = Build(r);
            if (instance is null) continue;
            result.Add(instance);
        }
        return result;
    }

    private static CustomFormatSpecBase? Build(RawSpec raw)
    {
        var fields = raw.Fields ?? new Dictionary<string, JsonElement>();
        CustomFormatSpecBase? spec = raw.Implementation switch
        {
            "ReleaseTitleSpecification" => new ReleaseTitleSpec { Pattern = fields.GetString("value") ?? fields.GetString("pattern") ?? string.Empty },
            "ReleaseGroupSpecification" => new ReleaseGroupSpec { Pattern = fields.GetString("value") ?? fields.GetString("pattern") ?? string.Empty },
            "IndexerFlagSpecification" => new IndexerFlagSpec { FlagKey = fields.GetString("flagKey") ?? fields.GetString("key") ?? string.Empty, ExpectedValue = fields.GetString("value") },
            "SourceSpecification" => new SourceSpec { ExpectedSource = ParseEnum<Source>(fields.GetString("source")) ?? Source.Unknown },
            "ResolutionSpecification" => new ResolutionSpec { ExpectedResolution = ParseEnum<Resolution>(fields.GetString("resolution")) ?? Resolution.Unknown },
            "LanguageSpecification" => new LanguageSpec { LanguageTag = fields.GetString("language") ?? fields.GetString("value") ?? string.Empty },
            "SizeSpecification" => new SizeSpec { MinBytes = fields.GetLong("minBytes"), MaxBytes = fields.GetLong("maxBytes") },
            "YouTubeChannelSpecification" => new YouTubeChannelSpec { ChannelMatch = fields.GetString("channel") ?? fields.GetString("value") ?? string.Empty },
            _ => null,
        };
        if (spec is null) return null;
        spec.Negate = raw.Negate ?? false;
        spec.Required = raw.Required ?? false;
        return spec;
    }

    private static T? ParseEnum<T>(string? raw) where T : struct, Enum =>
        Enum.TryParse<T>(raw, ignoreCase: true, out var v) ? v : null;

    private sealed record RawSpec(
        string? Implementation,
        bool? Negate,
        bool? Required,
        Dictionary<string, JsonElement>? Fields);
}

internal static class JsonElementExtensions
{
    public static string? GetString(this Dictionary<string, JsonElement> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }
    public static long? GetLong(this Dictionary<string, JsonElement> fields, string key)
    {
        if (!fields.TryGetValue(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetInt64(out var l) => l,
            JsonValueKind.String when long.TryParse(v.GetString(), out var l) => l,
            _ => null,
        };
    }
}
