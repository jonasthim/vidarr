using Vidarr.Contracts.Abstractions;
using Vidarr.Contracts.Models;

namespace Vidarr.Indexers;

/// <summary>
/// Torznab is NewzNab with extra torznab:attr fields (seeders, peers, magneturl).
/// Item parsing already handles those in the base class — we only need to flip the
/// protocol to Torrent so the downstream Importer routes the release correctly.
/// </summary>
public sealed class TorznabIndexer : NewznabIndexer
{
    public TorznabIndexer(int id, string name, NewznabIndexerSettings settings, IHttpClient http)
        : base(id, name, settings, http)
    {
    }

    public override DownloadProtocol Protocol => DownloadProtocol.Torrent;
}
