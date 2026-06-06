using System.Text.Json;
using System.Text.Json.Serialization;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.Metadata;

public sealed class ImvdbMetadataProvider : IMetadataProvider
{
    public const string ProviderId = "imvdb";

    private static readonly Uri BaseUri = new("https://imvdb.com/api/v1/");
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly IHttpClient _http;
    private readonly string? _apiKey;

    public ImvdbMetadataProvider(IHttpClient http, ImvdbOptions options)
    {
        _http = http;
        _apiKey = options.ApiKey;
    }

    public string Id => ProviderId;

    public async Task<IReadOnlyList<ArtistSearchResult>> SearchArtistsAsync(string query, CancellationToken ct)
    {
        var resp = await GetAsync($"search/entities?entity_type=artist&q={Uri.EscapeDataString(query)}", ct);
        if (resp.StatusCode != 200)
        {
            return [];
        }

        var doc = JsonSerializer.Deserialize<ImvdbSearchResponse>(resp.Body, JsonOpts);
        if (doc?.Results is null)
        {
            return [];
        }

        return [.. doc.Results.Select(r => new ArtistSearchResult(
            ProviderId: r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Name: r.Name ?? string.Empty,
            Disambiguation: null,
            FormedYear: null,
            Country: r.Country,
            ThumbnailUrl: r.Url is null ? null : new Uri(r.Url, UriKind.RelativeOrAbsolute)))];
    }

    public async Task<ArtistDetails> GetArtistAsync(string providerId, CancellationToken ct)
    {
        var resp = await GetAsync($"artist/{providerId}", ct);
        if (resp.StatusCode != 200)
        {
            throw new InvalidOperationException($"IMVDb artist lookup failed: HTTP {resp.StatusCode}");
        }

        var doc = JsonSerializer.Deserialize<ImvdbArtist>(resp.Body, JsonOpts)
            ?? throw new InvalidOperationException("IMVDb returned empty artist body");

        return new ArtistDetails(
            ProviderId: doc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Name: doc.Name ?? string.Empty,
            SortName: doc.Name ?? string.Empty,
            Disambiguation: null,
            Aliases: [],
            Genres: [],
            Country: doc.Country,
            YearsActiveStart: null,
            YearsActiveEnd: null,
            Images: doc.Image is null
                ? []
                : [new ArtistImage("poster", new Uri(doc.Image, UriKind.RelativeOrAbsolute))],
            ExternalIds: new Dictionary<string, string> { ["imvdb"] = doc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            YouTubeChannelIds: ExtractYouTubeChannelIds(doc.SocialLinks));
    }

    public async Task<IReadOnlyList<MusicVideoDetails>> GetArtistVideosAsync(string providerId, CancellationToken ct)
    {
        var resp = await GetAsync($"artist/{providerId}/videos", ct);
        if (resp.StatusCode != 200)
        {
            return [];
        }

        var doc = JsonSerializer.Deserialize<ImvdbVideosResponse>(resp.Body, JsonOpts);
        if (doc?.Videos is null)
        {
            return [];
        }

        return [.. doc.Videos.Select(v => MapVideo(v, providerId))];
    }

    public async Task<MusicVideoDetails> GetVideoAsync(string providerId, CancellationToken ct)
    {
        var resp = await GetAsync($"video/{providerId}", ct);
        if (resp.StatusCode != 200)
        {
            throw new InvalidOperationException($"IMVDb video lookup failed: HTTP {resp.StatusCode}");
        }

        var doc = JsonSerializer.Deserialize<ImvdbVideo>(resp.Body, JsonOpts)
            ?? throw new InvalidOperationException("IMVDb returned empty video body");

        var artistId = doc.Artists?.FirstOrDefault()?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return MapVideo(doc, artistId);
    }

    private static MusicVideoDetails MapVideo(ImvdbVideo v, string artistProviderId)
    {
        DateOnly? releaseDate = null;
        if (DateOnly.TryParse(v.ReleaseDate, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            releaseDate = d;
        }

        return new MusicVideoDetails(
            ProviderId: v.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ArtistProviderId: artistProviderId,
            Title: v.SongTitle ?? string.Empty,
            AlternateTitles: [],
            Year: v.Year,
            ReleaseDate: releaseDate,
            Type: MapType(v.Featured),
            Director: v.Directors?.FirstOrDefault()?.Name,
            ProductionCompany: null,
            Runtime: null,
            Genres: [],
            ThumbnailUrl: v.Image?.S is null ? null : new Uri(v.Image.S, UriKind.RelativeOrAbsolute),
            ExternalIds: new Dictionary<string, string> { ["imvdb"] = v.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) });
    }

    private static MusicVideoType MapType(string? featured) => featured switch
    {
        "lyric" => MusicVideoType.Lyric,
        "live" => MusicVideoType.Live,
        "acoustic" => MusicVideoType.Acoustic,
        _ => MusicVideoType.Official,
    };

    private static IReadOnlyList<string> ExtractYouTubeChannelIds(IReadOnlyList<ImvdbSocialLink>? links)
    {
        if (links is null)
        {
            return [];
        }

        return [.. links
            .Where(l => string.Equals(l.Type, "youtube", StringComparison.OrdinalIgnoreCase))
            .Select(l => ExtractChannelId(l.Url))
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)];
    }

    private static string? ExtractChannelId(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }
        var marker = "/channel/";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }
        var rest = url[(idx + marker.Length)..];
        var slash = rest.IndexOfAny(['/', '?', '#']);
        return slash >= 0 ? rest[..slash] : rest;
    }

    private Task<HttpClientResponse> GetAsync(string relative, CancellationToken ct)
    {
        var uri = new Uri(BaseUri, relative);
        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json",
            ["User-Agent"] = "Vidarr/1.0",
        };
        if (!string.IsNullOrEmpty(_apiKey))
        {
            headers["IMVDB-APP-KEY"] = _apiKey;
        }
        return _http.SendAsync(new HttpClientRequest(HttpMethod.Get, uri, headers), ct);
    }

    private sealed record ImvdbSearchResponse([property: JsonPropertyName("results")] List<ImvdbSearchHit>? Results);

    private sealed record ImvdbSearchHit(long Id, string? Name, string? Country, string? Url);

    private sealed record ImvdbArtist(long Id, string? Name, string? Country, string? Image, [property: JsonPropertyName("url_slug")] string? UrlSlug, [property: JsonPropertyName("social_links")] List<ImvdbSocialLink>? SocialLinks);

    private sealed record ImvdbSocialLink(string? Type, string? Url);

    private sealed record ImvdbVideosResponse(List<ImvdbVideo>? Videos);

    private sealed record ImvdbVideo(
        long Id,
        [property: JsonPropertyName("song_title")] string? SongTitle,
        int? Year,
        [property: JsonPropertyName("release_date")] string? ReleaseDate,
        string? Featured,
        List<ImvdbDirector>? Directors,
        List<ImvdbVideoArtist>? Artists,
        ImvdbImage? Image);

    private sealed record ImvdbDirector(string? Name);

    private sealed record ImvdbVideoArtist(long Id, string? Name);

    private sealed record ImvdbImage([property: JsonPropertyName("s")] string? S);
}

public sealed record ImvdbOptions(string? ApiKey)
{
    public static ImvdbOptions Empty { get; } = new((string?)null);
}
