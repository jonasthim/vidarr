using Vidarr.Contracts.Models;

namespace Vidarr.Decision;

public interface IDecisionEngine
{
    IReadOnlyList<RemoteRelease> Decide(IEnumerable<ReleaseInfo> releases, DecisionContext ctx);
}

public sealed class DecisionEngine : IDecisionEngine
{
    private readonly IReleaseParser _parser;
    private readonly IReadOnlyList<IReleaseSpec> _specs;
    private readonly IReleaseComparer _comparer;

    public DecisionEngine(IReleaseParser parser, IEnumerable<IReleaseSpec> specs, IReleaseComparer comparer)
    {
        _parser = parser;
        _specs = [.. specs];
        _comparer = comparer;
    }

    public IReadOnlyList<RemoteRelease> Decide(IEnumerable<ReleaseInfo> releases, DecisionContext ctx)
    {
        var evaluated = new List<RemoteRelease>();
        foreach (var release in releases)
        {
            var parsed = _parser.Parse(release.Title);
            var rejections = new List<string>();
            foreach (var spec in _specs)
            {
                if (!spec.IsSatisfied(release, parsed, ctx, out var reason))
                {
                    rejections.Add(reason ?? spec.Name);
                }
            }
            evaluated.Add(new RemoteRelease(release, parsed, Score: 0, rejections, MatchedMusicVideoIds: []));
        }

        return [.. evaluated.OrderBy(r => r, _comparer)];
    }
}

public interface IReleaseComparer : IComparer<RemoteRelease>
{
}

/// <summary>
/// Caller-supplied protocol preference. The list is read left-to-right: the protocol
/// appearing earliest wins. Defaults match the Sonarr convention (Usenet preferred —
/// faster and more reliable than torrents in steady state — Torrent second, Streaming
/// last because YouTube quality varies wildly per upload).
/// </summary>
public sealed record ProtocolPreference(IReadOnlyList<DownloadProtocol> Order)
{
    public static readonly ProtocolPreference Default = new([
        DownloadProtocol.Usenet, DownloadProtocol.Torrent, DownloadProtocol.Streaming,
    ]);

    public int Rank(DownloadProtocol p)
    {
        for (var i = 0; i < Order.Count; i++)
        {
            if (Order[i] == p) return i;
        }
        return Order.Count;
    }
}

public sealed class DefaultReleaseComparer : IReleaseComparer
{
    private readonly QualityProfile _profile;
    private readonly ProtocolPreference _protocolPreference;

    public DefaultReleaseComparer(QualityProfile profile, ProtocolPreference? protocolPreference = null)
    {
        _profile = profile;
        _protocolPreference = protocolPreference ?? ProtocolPreference.Default;
    }

    public int Compare(RemoteRelease? x, RemoteRelease? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        // Rejected releases sort last.
        var xRejected = x.RejectionReasons.Count > 0;
        var yRejected = y.RejectionReasons.Count > 0;
        if (xRejected != yRejected)
        {
            return xRejected ? 1 : -1;
        }

        // Higher quality rank first.
        var xRank = UpgradeAllowedSpec.QualityRank(x.Parsed.Quality, _profile);
        var yRank = UpgradeAllowedSpec.QualityRank(y.Parsed.Quality, _profile);
        if (xRank != yRank)
        {
            return yRank.CompareTo(xRank);
        }

        // Configured protocol preference next.
        var xProto = _protocolPreference.Rank(x.Info.Protocol);
        var yProto = _protocolPreference.Rank(y.Info.Protocol);
        if (xProto != yProto)
        {
            return xProto.CompareTo(yProto);
        }

        // Protocol-specific tie-break: Usenet → smaller (younger) age wins; Torrent →
        // larger seeder count wins; everything else → larger size as the proxy.
        if (x.Info.Protocol == DownloadProtocol.Usenet && y.Info.Protocol == DownloadProtocol.Usenet)
        {
            var xAge = x.Info.Age ?? TimeSpan.MaxValue;
            var yAge = y.Info.Age ?? TimeSpan.MaxValue;
            if (xAge != yAge)
            {
                return xAge.CompareTo(yAge);
            }
        }
        else if (x.Info.Protocol == DownloadProtocol.Torrent && y.Info.Protocol == DownloadProtocol.Torrent)
        {
            var xSeed = x.Info.Seeders ?? 0;
            var ySeed = y.Info.Seeders ?? 0;
            if (xSeed != ySeed)
            {
                return ySeed.CompareTo(xSeed);
            }
        }

        // Larger size wins as a final tie-break.
        var xSize = x.Info.SizeBytes ?? 0;
        var ySize = y.Info.SizeBytes ?? 0;
        return ySize.CompareTo(xSize);
    }
}
