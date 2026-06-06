using System.Globalization;
using System.Web;
using System.Xml.Linq;
using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;

namespace Vidarr.Indexers;

/// <summary>
/// NewzNab + Torznab share the same response shape — Torznab just decorates each item
/// with extra <c>&lt;torznab:attr&gt;</c> fields (seeders, peers, magneturl). NewznabIndexer
/// handles the NewzNab subset; <see cref="TorznabIndexer"/> derives from this class.
/// </summary>
public class NewznabIndexer : IIndexer
{
    private static readonly XNamespace Newznab = "http://www.newznab.com/DTD/2010/feeds/attributes/";
    private static readonly XNamespace Torznab = "http://torznab.com/schemas/2015/feed";

    private readonly IHttpClient _http;
    private readonly NewznabIndexerSettings _settings;

    public NewznabIndexer(int id, string name, NewznabIndexerSettings settings, IHttpClient http)
    {
        Id = id;
        Name = name;
        _settings = settings;
        _http = http;
    }

    public int Id { get; }
    public string Name { get; }
    public virtual DownloadProtocol Protocol => DownloadProtocol.Usenet;
    public bool SupportsRss => true;
    public bool SupportsSearch => true;

    public async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(IndexerSearchCriteria criteria, CancellationToken ct)
    {
        var query = !string.IsNullOrEmpty(criteria.ArtistName) && !string.IsNullOrEmpty(criteria.Title)
            ? $"{criteria.ArtistName} {criteria.Title}"
            : criteria.Query;
        return await FetchInternalAsync("search", query, ct);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> RssSyncAsync(CancellationToken ct) =>
        await FetchInternalAsync("search", string.Empty, ct);

    public async Task<IndexerTestResult> TestAsync(CancellationToken ct)
    {
        var url = BuildUrl("caps", null);
        var resp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Get, url, BuildHeaders(), Timeout: _settings.Timeout), ct);
        return resp.StatusCode is >= 200 and < 300
            ? new IndexerTestResult(true, "OK")
            : new IndexerTestResult(false, $"HTTP {resp.StatusCode}");
    }

    private async Task<IReadOnlyList<ReleaseInfo>> FetchInternalAsync(string t, string? query, CancellationToken ct)
    {
        var url = BuildUrl(t, query);
        var resp = await _http.SendAsync(new HttpClientRequest(HttpMethod.Get, url, BuildHeaders(), Timeout: _settings.Timeout), ct);
        if (resp.StatusCode != 200 || string.IsNullOrEmpty(resp.Body))
        {
            return [];
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(resp.Body);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var items = doc.Descendants("item").Select(ParseItem).Where(r => r is not null).Select(r => r!).ToList();
        items = ApplyMinMaxAge(items);
        return items;
    }

    private List<ReleaseInfo> ApplyMinMaxAge(List<ReleaseInfo> items)
    {
        if (_settings.MinAgeMinutes is null && _settings.MaxAgeDays is null)
        {
            return items;
        }

        return [.. items.Where(r =>
        {
            if (r.Age is null) return true;
            if (_settings.MinAgeMinutes is { } min && r.Age.Value.TotalMinutes < min) return false;
            if (_settings.MaxAgeDays is { } max && r.Age.Value.TotalDays > max) return false;
            return true;
        })];
    }

    protected virtual ReleaseInfo? ParseItem(XElement item)
    {
        var title = item.Element("title")?.Value?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        var link = item.Element("link")?.Value ?? string.Empty;
        if (!Uri.TryCreate(link, UriKind.Absolute, out var sourceUrl))
        {
            return null;
        }

        DateTimeOffset? published = null;
        if (DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var pub))
        {
            published = pub.ToUniversalTime();
        }

        var size = item.Element("size") is { Value: var sStr } && long.TryParse(sStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sLong)
            ? (long?)sLong
            : null;

        var newznabAttrs = item.Elements(Newznab + "attr").ToDictionary(
            e => e.Attribute("name")?.Value ?? string.Empty,
            e => e.Attribute("value")?.Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
        var torznabAttrs = item.Elements(Torznab + "attr").ToDictionary(
            e => e.Attribute("name")?.Value ?? string.Empty,
            e => e.Attribute("value")?.Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        // The newznab <attr name="size"> may carry the exact size even when <size> is missing.
        if (size is null && newznabAttrs.TryGetValue("size", out var sizeAttr)
            && long.TryParse(sizeAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sFromAttr))
        {
            size = sFromAttr;
        }

        int? seeders = null;
        int? leechers = null;
        string? magnet = null;
        if (torznabAttrs.TryGetValue("seeders", out var seederStr)
            && int.TryParse(seederStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sd))
        {
            seeders = sd;
        }
        if (torznabAttrs.TryGetValue("peers", out var peerStr)
            && int.TryParse(peerStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pd))
        {
            leechers = Math.Max(0, pd - (seeders ?? 0));
        }
        if (torznabAttrs.TryGetValue("magneturl", out var m))
        {
            magnet = m;
        }

        TimeSpan? age = published is { } p ? DateTimeOffset.UtcNow - p : null;

        var category = newznabAttrs.GetValueOrDefault("category");

        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in newznabAttrs) extras[$"newznab.{k}"] = v;
        foreach (var (k, v) in torznabAttrs) extras[$"torznab.{k}"] = v;
        var guid = item.Element("guid")?.Value;
        if (!string.IsNullOrEmpty(guid)) extras["guid"] = guid;

        return new ReleaseInfo(
            Title: title,
            SourceUrl: sourceUrl,
            Magnet: magnet,
            SizeBytes: size,
            PublishedAt: published,
            Age: age,
            Seeders: seeders,
            Leechers: leechers,
            Protocol: Protocol,
            IndexerName: Name,
            IndexerCategory: category,
            ExtraMetadata: extras);
    }

    private Uri BuildUrl(string t, string? query)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["t"] = t;
        qs["apikey"] = _settings.ApiKey ?? string.Empty;
        if (!string.IsNullOrEmpty(query))
        {
            qs["q"] = query;
        }
        if (_settings.Categories.Count > 0)
        {
            qs["cat"] = string.Join(',', _settings.Categories);
        }
        if (_settings.MinAgeMinutes is { } min)
        {
            qs["minage"] = min.ToString(CultureInfo.InvariantCulture);
        }
        if (_settings.MaxAgeDays is { } max)
        {
            qs["maxage"] = max.ToString(CultureInfo.InvariantCulture);
        }
        var baseUri = _settings.BaseUrl.AbsoluteUri.TrimEnd('/');
        return new Uri($"{baseUri}/api?{qs}");
    }

    private static Dictionary<string, string> BuildHeaders() => new()
    {
        ["User-Agent"] = "Vidarr/1.0",
        ["Accept"] = "application/xml,text/xml",
    };
}

public sealed record NewznabIndexerSettings(
    Uri BaseUrl,
    string? ApiKey,
    IReadOnlyList<int> Categories,
    int? MinAgeMinutes = null,
    int? MaxAgeDays = null,
    TimeSpan? Timeout = null)
{
    public static NewznabIndexerSettings WithDefaultCategories(Uri baseUrl, string? apiKey) =>
        new(baseUrl, apiKey, [6030]);
}
