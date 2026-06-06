using Vidarr.Contracts.Models;

namespace Vidarr.Contracts.Domain;

public interface IMetadataProvider
{
    string Id { get; }
    Task<IReadOnlyList<ArtistSearchResult>> SearchArtistsAsync(string query, CancellationToken ct);
    Task<ArtistDetails> GetArtistAsync(string providerId, CancellationToken ct);
    Task<IReadOnlyList<MusicVideoDetails>> GetArtistVideosAsync(string providerId, CancellationToken ct);
    Task<MusicVideoDetails> GetVideoAsync(string providerId, CancellationToken ct);
}
