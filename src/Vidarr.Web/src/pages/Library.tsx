import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Music, RefreshCw, Search } from "lucide-react";
import { api, type ReleaseSearchItem } from "../api";
import { PageHeader, Card, StatusPill, EmptyState } from "../components/ui";

export function LibraryPage(): JSX.Element {
  const [selectedArtistId, setSelectedArtistId] = useState<number | null>(null);
  const [channelsDraft, setChannelsDraft] = useState<string>("");
  const [filter, setFilter] = useState("");
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

  const filtered = useMemo(() => {
    if (!artists.data) return [];
    if (!filter) return artists.data;
    const lower = filter.toLowerCase();
    return artists.data.filter((a) => a.name.toLowerCase().includes(lower));
  }, [artists.data, filter]);

  return (
    <>
      <PageHeader
        title="Library"
        subtitle={`${artists.data?.length ?? 0} artist${artists.data?.length === 1 ? "" : "s"} monitored`}
        actions={
          selectedArtistId !== null && (
            <button
              type="button"
              className="primary"
              disabled={search.isPending}
              onClick={() => search.mutate(selectedArtistId)}
            >
              <Search size={14} />
              {search.isPending ? "Searching…" : "Search now"}
            </button>
          )
        }
      />

      <div className="library-grid">
        <div className="library-list">
          <div className="library-list-toolbar">
            <Search size={14} className="muted" />
            <input
              type="search"
              placeholder="Filter artists…"
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
            />
          </div>
          {artists.isLoading && <div className="loading-state">Loading…</div>}
          {artists.data && artists.data.length === 0 && (
            <EmptyState
              icon={<Music />}
              title="No artists yet"
              description="Use Add Artist to start your library."
            />
          )}
          <ul>
            {filtered.map((a) => (
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
                <div>
                  <strong>{a.name}</strong>
                  {a.country && <span className="muted"> · {a.country}</span>}
                </div>
                <StatusPill variant={a.monitored ? "monitored" : "unmonitored"}>
                  {a.monitored ? "Monitored" : "Unmonitored"}
                </StatusPill>
              </li>
            ))}
          </ul>
        </div>

        <div className="library-detail">
          {selectedArtistId === null ? (
            <EmptyState
              icon={<Music />}
              title="Select an artist"
              description="Pick an artist on the left to see their videos and trigger searches."
            />
          ) : (
            <>
              <Card title="YouTube channels">
                <div className="form-row">
                  <label htmlFor="yt-channels">
                    Comma-separated channel IDs (UC…). Used for RSS sync.
                  </label>
                  <input
                    id="yt-channels"
                    value={channelsDraft}
                    onChange={(e) => setChannelsDraft(e.target.value)}
                    placeholder="UCabc, UCdef"
                  />
                </div>
                <button
                  type="button"
                  className="primary"
                  disabled={saveChannels.isPending}
                  onClick={() =>
                    saveChannels.mutate({
                      id: selectedArtistId,
                      channels: channelsDraft.split(",").map((c) => c.trim()).filter(Boolean),
                    })
                  }
                >
                  Save channels
                </button>
              </Card>

              <Card title={`Music videos (${videos.data?.length ?? 0})`}>
                {videos.isLoading && <div className="loading-state">Loading…</div>}
                {videos.data && videos.data.length === 0 && (
                  <EmptyState
                    icon={<Music />}
                    title="No videos yet"
                    description="Trigger Search now to pull from configured indexers."
                  />
                )}
                {videos.data && videos.data.length > 0 && (
                  <table className="grid">
                    <thead>
                      <tr>
                        <th>Title</th>
                        <th>Year</th>
                        <th>Status</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {videos.data.map((v) => (
                        <tr key={v.id}>
                          <td>{v.title}</td>
                          <td>{v.year ?? "—"}</td>
                          <td>
                            {v.hasFile ? (
                              <StatusPill variant="success">Downloaded</StatusPill>
                            ) : v.monitored ? (
                              <StatusPill variant="warning">Wanted</StatusPill>
                            ) : (
                              <StatusPill variant="unmonitored">Ignored</StatusPill>
                            )}
                          </td>
                          <td className="actions">
                            <button
                              type="button"
                              onClick={() => {
                                setSearchVideoId(v.id);
                                setSearchResults([]);
                                interactive.mutate({ artistId: selectedArtistId, musicVideoId: v.id });
                              }}
                            >
                              <Search size={14} />
                              Interactive search
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </Card>

              {searchVideoId !== null && (
                <Card
                  title={`Releases for video #${searchVideoId}`}
                  actions={
                    <button
                      type="button"
                      className="ghost"
                      onClick={() => {
                        setSearchVideoId(null);
                        setSearchResults([]);
                      }}
                    >
                      Close
                    </button>
                  }
                >
                  {interactive.isPending && <div className="loading-state">Searching…</div>}
                  {!interactive.isPending && searchResults.length === 0 && (
                    <EmptyState
                      icon={<RefreshCw />}
                      title="No releases"
                      description="No indexer returned a match. Verify indexer config and try again."
                    />
                  )}
                  {searchResults.length > 0 && (
                    <table className="grid">
                      <thead>
                        <tr>
                          <th>Title</th>
                          <th>Indexer</th>
                          <th>Protocol</th>
                          <th>Seeders</th>
                          <th>Size</th>
                          <th></th>
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
                            <td className="actions">
                              <button
                                type="button"
                                className="primary"
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
                  )}
                </Card>
              )}
            </>
          )}
        </div>
      </div>
    </>
  );
}
