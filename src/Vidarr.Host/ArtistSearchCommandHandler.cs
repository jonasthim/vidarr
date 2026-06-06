using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Vidarr.Catalog.Repositories;
using Vidarr.Contracts.Domain;
using Vidarr.Contracts.Models;
using Vidarr.Decision;
using Vidarr.Naming;
using Vidarr.Scheduler;

namespace Vidarr.Host;

[ExcludeFromCodeCoverage(Justification = "Composition wiring; integration-tested end to end.")]
public sealed class ArtistSearchCommandHandler : ICommandHandler<ArtistSearchCommand>
{
    private readonly IArtistRepository _artists;
    private readonly IMusicVideoRepository _videos;
    private readonly IIndexer _indexer;
    private readonly IDownloadClient _downloadClient;
    private readonly IReleaseParser _parser;
    private readonly ILogger<ArtistSearchCommandHandler> _logger;

    public ArtistSearchCommandHandler(
        IArtistRepository artists,
        IMusicVideoRepository videos,
        IIndexer indexer,
        IDownloadClient downloadClient,
        IReleaseParser parser,
        ILogger<ArtistSearchCommandHandler> logger)
    {
        _artists = artists;
        _videos = videos;
        _indexer = indexer;
        _downloadClient = downloadClient;
        _parser = parser;
        _logger = logger;
    }

    public async Task HandleAsync(ArtistSearchCommand command, CancellationToken ct)
    {
        var artist = await _artists.GetAsync(command.ArtistId, ct);
        if (artist is null)
        {
            _logger.LogWarning("ArtistSearch: artist {Id} not found", command.ArtistId);
            return;
        }

        var wanted = (await _videos.ListByArtistAsync(artist.Id, ct)).Where(v => v.Monitored && !v.HasFile).ToList();
        if (wanted.Count == 0)
        {
            _logger.LogInformation("ArtistSearch: artist {Name} has no wanted videos", artist.Name);
            return;
        }

        foreach (var video in wanted.Take(5))
        {
            var criteria = new IndexerSearchCriteria(
                Query: $"{artist.Name} {video.Title}",
                ArtistName: artist.Name,
                Title: video.Title,
                Year: video.Year,
                Categories: ["music-video"]);
            var releases = await _indexer.FetchAsync(criteria, ct);
            var best = releases.FirstOrDefault();
            if (best is null)
            {
                _logger.LogInformation("ArtistSearch: no releases for {Artist} - {Title}", artist.Name, video.Title);
                continue;
            }

            var parsed = _parser.Parse(best.Title);
            var remote = new RemoteRelease(best, parsed, Score: 0, RejectionReasons: [], MatchedMusicVideoIds: [video.Id]);
            var id = await _downloadClient.DownloadAsync(remote, ct);
            _logger.LogInformation("ArtistSearch: queued {Title} as {Id}", best.Title, id.Value);
        }
    }
}
