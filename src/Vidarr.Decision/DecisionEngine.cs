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

public sealed class DefaultReleaseComparer : IReleaseComparer
{
    private readonly QualityProfile _profile;

    public DefaultReleaseComparer(QualityProfile profile)
    {
        _profile = profile;
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

        // Larger seeder counts win for torrents.
        var xSeed = x.Info.Seeders ?? 0;
        var ySeed = y.Info.Seeders ?? 0;
        if (xSeed != ySeed)
        {
            return ySeed.CompareTo(xSeed);
        }

        // Larger size wins as a final tie-break.
        var xSize = x.Info.SizeBytes ?? 0;
        var ySize = y.Info.SizeBytes ?? 0;
        return ySize.CompareTo(xSize);
    }
}
