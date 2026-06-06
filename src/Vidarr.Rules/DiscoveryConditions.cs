using System.Text.Json;
using Vidarr.Contracts.Models;

namespace Vidarr.Rules;

public sealed record DiscoveryContext(
    int? Year,
    IReadOnlyList<string> Genres,
    string? Country,
    MusicVideoType Type);

public interface IDiscoveryCondition
{
    string Type { get; }
    bool Matches(DiscoveryContext ctx);
}

internal sealed class GenreInCondition : IDiscoveryCondition
{
    public string Type => "GenreIn";
    public required IReadOnlyList<string> Values { get; init; }
    public bool Matches(DiscoveryContext ctx) =>
        Values.Any(v => ctx.Genres.Any(g => string.Equals(g, v, StringComparison.OrdinalIgnoreCase)));
}

internal sealed class YearGteCondition : IDiscoveryCondition
{
    public string Type => "YearGte";
    public required int Value { get; init; }
    public bool Matches(DiscoveryContext ctx) => ctx.Year is { } y && y >= Value;
}

internal sealed class YearLteCondition : IDiscoveryCondition
{
    public string Type => "YearLte";
    public required int Value { get; init; }
    public bool Matches(DiscoveryContext ctx) => ctx.Year is { } y && y <= Value;
}

internal sealed class DecadeEqCondition : IDiscoveryCondition
{
    public string Type => "DecadeEq";
    public required int Value { get; init; }
    public bool Matches(DiscoveryContext ctx) => ctx.Year is { } y && (y / 10 * 10) == (Value / 10 * 10);
}

internal sealed class TypeInCondition : IDiscoveryCondition
{
    public string Type => "TypeIn";
    public required IReadOnlyList<string> Values { get; init; }
    public bool Matches(DiscoveryContext ctx)
    {
        var typeName = ctx.Type.ToString();
        return Values.Any(v => string.Equals(v, typeName, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class CountryInCondition : IDiscoveryCondition
{
    public string Type => "CountryIn";
    public required IReadOnlyList<string> Values { get; init; }
    public bool Matches(DiscoveryContext ctx) =>
        ctx.Country is not null && Values.Any(v => string.Equals(v, ctx.Country, StringComparison.OrdinalIgnoreCase));
}

public static class DiscoveryConditionParser
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<IDiscoveryCondition> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
        {
            return [];
        }
        List<RawCondition>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<List<RawCondition>>(json, JsonOpts);
        }
        catch (JsonException)
        {
            return [];
        }
        if (raw is null) return [];

        var result = new List<IDiscoveryCondition>();
        foreach (var r in raw)
        {
            var c = Build(r);
            if (c is not null) result.Add(c);
        }
        return result;
    }

    private static IDiscoveryCondition? Build(RawCondition raw)
    {
        if (string.IsNullOrEmpty(raw.Type)) return null;
        return raw.Type.ToLowerInvariant() switch
        {
            "genrein" => new GenreInCondition { Values = NormaliseStrings(raw.Values) },
            "yeargte" => raw.Value is { } yg ? new YearGteCondition { Value = yg } : null,
            "yearlte" => raw.Value is { } yl ? new YearLteCondition { Value = yl } : null,
            "decadeeq" => raw.Value is { } d ? new DecadeEqCondition { Value = d } : null,
            "typein" => new TypeInCondition { Values = NormaliseStrings(raw.Values) },
            "countryin" => new CountryInCondition { Values = NormaliseStrings(raw.Values) },
            _ => null,
        };
    }

    private static IReadOnlyList<string> NormaliseStrings(List<string>? values) =>
        values is null ? [] : [.. values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim())];

    private sealed record RawCondition(string? Type, int? Value, List<string>? Values);
}

public sealed record DiscoveryAction(
    int? QualityProfileId,
    string? RootFolderPath,
    MonitorMode? MonitorMode,
    IReadOnlyList<int> Tags);

public static class DiscoveryActionParser
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static DiscoveryAction Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new DiscoveryAction(null, null, null, []);
        }
        RawAction? raw;
        try
        {
            raw = JsonSerializer.Deserialize<RawAction>(json, JsonOpts);
        }
        catch (JsonException)
        {
            return new DiscoveryAction(null, null, null, []);
        }
        if (raw is null)
        {
            return new DiscoveryAction(null, null, null, []);
        }
        MonitorMode? mode = Enum.TryParse<MonitorMode>(raw.MonitorMode, ignoreCase: true, out var m) ? m : null;
        return new DiscoveryAction(
            QualityProfileId: raw.QualityProfileId,
            RootFolderPath: string.IsNullOrWhiteSpace(raw.RootFolderPath) ? null : raw.RootFolderPath,
            MonitorMode: mode,
            Tags: raw.Tags ?? []);
    }

    private sealed record RawAction(int? QualityProfileId, string? RootFolderPath, string? MonitorMode, List<int>? Tags);
}
