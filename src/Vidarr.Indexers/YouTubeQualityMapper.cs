using Vidarr.Contracts.Models;

namespace Vidarr.Indexers;

public interface IYouTubeQualityMapper
{
    /// <summary>
    /// Maps a YouTube upload's reported max height (in pixels) onto a canonical
    /// <see cref="Quality"/>. Codec is informational only — per spec §8, YouTube
    /// uploads always land on the WEBDL ladder regardless of VP9/AV1/H.264.
    /// </summary>
    Quality FromHeight(int? height);
}

public sealed class YouTubeQualityMapper : IYouTubeQualityMapper
{
    public Quality FromHeight(int? height) => height switch
    {
        null => Quality.Unknown,
        >= 2160 => Quality.Webdl2160p,
        >= 1080 => Quality.Webdl1080p,
        >= 720 => Quality.Webdl720p,
        > 0 => Quality.Webdl480p,
        _ => Quality.Unknown,
    };
}
