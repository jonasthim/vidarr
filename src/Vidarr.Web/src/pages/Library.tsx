import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type ReleaseSearchItem } from "../api";

export function LibraryPage(): JSX.Element {
  const [selectedArtistId, setSelectedArtistId] = useState<number | null>(null);
  const [channelsDraft, setChannelsDraft] = useState<string>("");
  const queryClient = useQueryClient();

  const artists = useQuery({
    queryKey: ["artists"],
    queryFn: api.listArtists,
  });

  const selectedArtist = artists.data?.find((a) => a.id === selectedArtistId);

  useEffect(() => {
    if (selectedArtist) {
      setChannelsDraft(selectedArtist.youTubeChannelIds.join(", "));
    }
  }, [selectedArtist]);

  const videos = useQuery({
    queryKey: ["videos", selectedArtistId],
    queryFn: () => api.listMusicVideos(selectedArtistId!),
    enabled: selectedArtistId !== null,
  });

  const search = useMutation({
    mutationFn: (artistId: number) => api.triggerArtistSearch(artistId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["queue"] }),
  });

  const saveChannels = useMutation({
    mutationFn: ({ id, channels }: { id: number; channels: string[] }) =>
      api.updateYouTubeChannels(id, channels),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["artists"] }),
  });

  const [searchVideoId, setSearchVideoId] = useState<number | null>(null);
  const [searchResults, setSearchResults] = useState<ReleaseSearchItem[]>([]);
  const interactive = useMutation({
    mutationFn: ({ artistId, musicVideoId }: { artistId: number; musicVideoId: number }) =>
      api.searchReleases({ artistId, musicVideoId }),
    onSuccess: (env) => setSearchResults(env.releases),
  });
  const grab = useMutation({
    mutationFn: (item: ReleaseSearchItem) =>
      api.grabRelease({
        title: item.title,
        sourceUrl: item.sourceUrl,
        magnet: item.magnet,
        sizeBytes: item.sizeBytes,
        publishedAt: item.publishedAt,
        seeders: item.seeders,
        leechers: item.leechers,
        protocol: item.protocol,
        indexerName: item.indexerName,
        indexerCategory: item.indexerCategory,
        musicVideoIds: searchVideoId ? [searchVideoId] : undefined,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["queue"] }),
  });

  return (
    <section className="library">
      <div className="artists">
        <h2>Artists ({artists.data?.length ?? 0})</h2>
        {artists.isLoading && <p>Loading...</p>}
        <ul>
          {artists.data?.map((a) => (
            <li
              key={a.id}
              onClick={() => setSelectedArtistId(a.id)}
              className={selectedArtistId === a.id ? "selected" : ""}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => {
                if (e.key === "Enter") setSelectedArtistId(a.id);
              }}
            >
              <strong>{a.name}</strong>
              {a.country && <span> · {a.country}</span>}
            </li>
          ))}
        </ul>
      </div>
      <div className="videos">
        {selectedArtistId !== null && (
          <>
            <header>
              <h2>Videos</h2>
              <button
                type="button"
                disabled={search.isPending}
                onClick={() => search.mutate(selectedArtistId)}
              >
                Search now
              </button>
            </header>

            <section className="youtube-channels">
              <label>
                YouTube channels (comma-separated UC… IDs)
                <input
                  value={channelsDraft}
                  onChange={(e) => setChannelsDraft(e.target.value)}
                  placeholder="UCabc, UCdef"
                />
              </label>
              <button
                type="button"
                disabled={saveChannels.isPending}
                onClick={() =>
                  saveChannels.mutate({
                    id: selectedArtistId,
                    channels: channelsDraft
                      .split(",")
                      .map((c) => c.trim())
                      .filter(Boolean),
                  })
                }
              >
                Save channels
              </button>
            </section>
            <ul>
              {videos.data?.map((v) => (
                <li key={v.id}>
                  <strong>{v.title}</strong>
                  {v.year && <span> ({v.year})</span>} ·{" "}
                  {v.hasFile ? "downloaded" : v.monitored ? "wanted" : "ignored"}
                  <button
                    type="button"
                    onClick={() => {
                      setSearchVideoId(v.id);
                      setSearchResults([]);
                      interactive.mutate({ artistId: selectedArtistId, musicVideoId: v.id });
                    }}
                  >
                    Interactive search
                  </button>
                </li>
              ))}
            </ul>

            {searchVideoId !== null && (
              <section className="interactive-search">
                <h3>Releases for video #{searchVideoId}</h3>
                {interactive.isPending && <p>Searching…</p>}
                {searchResults.length === 0 && !interactive.isPending && <p>No results yet.</p>}
                <table>
                  <thead>
                    <tr>
                      <th>Title</th>
                      <th>Indexer</th>
                      <th>Protocol</th>
                      <th>Seeders</th>
                      <th>Size</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {searchResults.map((r, idx) => (
                      <tr key={idx}>
                        <td>{r.title}</td>
                        <td>{r.indexerName}</td>
                        <td>{r.protocol}</td>
                        <td>{r.seeders ?? "—"}</td>
                        <td>{r.sizeBytes ? `${(r.sizeBytes / 1_000_000).toFixed(1)} MB` : "—"}</td>
                        <td>
                          <button
                            type="button"
                            disabled={grab.isPending}
                            onClick={() => grab.mutate(r)}
                          >
                            Grab
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </section>
            )}
          </>
        )}
      </div>
    </section>
  );
}
