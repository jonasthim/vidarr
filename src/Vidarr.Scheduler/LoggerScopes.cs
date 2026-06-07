using Microsoft.Extensions.Logging;

namespace Vidarr.Scheduler;

/// <summary>
/// Helpers for pushing structured properties onto ILogger.BeginScope. Serilog's
/// FromLogContext enricher picks these up so log events emitted during the scope
/// carry the property as a top-level field.
/// </summary>
public static class LoggerScopes
{
    public static IDisposable? Artist(this ILogger logger, int artistId) =>
        logger.BeginScope(new Dictionary<string, object> { ["ArtistId"] = artistId });

    public static IDisposable? MusicVideo(this ILogger logger, int musicVideoId) =>
        logger.BeginScope(new Dictionary<string, object> { ["MusicVideoId"] = musicVideoId });

    public static IDisposable? Indexer(this ILogger logger, string indexerName) =>
        logger.BeginScope(new Dictionary<string, object> { ["IndexerName"] = indexerName });

    public static IDisposable? DownloadClient(this ILogger logger, string clientName) =>
        logger.BeginScope(new Dictionary<string, object> { ["DownloadClient"] = clientName });
}
