import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, type ReleaseSearchItem } from "../api";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
import { PageToolbarSeparator } from "../Components/Page/Toolbar/PageToolbarSeparator";
import { Card, StatusPill, EmptyState } from "../components/ui";
import { pickPoster, pickBanner } from "../components/ArtistImageHelpers";

function formatRuntime(seconds?: number | null): string {
  if (!seconds) return "—";
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return s === 0 ? `${m}m` : `${m}m ${s}s`;
}

export function ArtistDetail(): JSX.Element {
  const { artistId } = useParams<{ artistId: string }>();
  const id = artistId ? Number.parseInt(artistId, 10) : NaN;
  const queryClient = useQueryClient();

  const detailsQuery = useQuery({
    queryKey: ["artistDetails", id],
    queryFn: () => api.getArtistDetails(id),
    enabled: Number.isFinite(id),
    refetchInterval: false,
  });

  const videosQuery = useQuery({
    queryKey: ["videos", id],
    queryFn: () => api.listMusicVideos(id),
    enabled: Number.isFinite(id),
  });

  const search = useMutation({
    mutationFn: (n: number) => api.triggerArtistSearch(n),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["queue"] }),
  });

  const [channelsDraft, setChannelsDraft] = useState("");
  useEffect(() => {
    if (detailsQuery.data) {
      setChannelsDraft(detailsQuery.data.artist.youTubeChannelIds.join(", "));
    }
  }, [detailsQuery.data]);

  const saveChannels = useMutation({
    mutationFn: (channels: string[]) => api.updateYouTubeChannels(id, channels),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["artistDetails", id] });
      queryClient.invalidateQueries({ queryKey: ["artists"] });
    },
  });

  const [searchVideoId, setSearchVideoId] = useState<number | null>(null);
  const [searchResults, setSearchResults] = useState<ReleaseSearchItem[]>([]);
  const interactive = useMutation({
    mutationFn: ({ musicVideoId }: { musicVideoId: number }) =>
      api.searchReleases({ artistId: id, musicVideoId }),
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

  if (detailsQuery.isLoading) {
    return (
      <PageContent title="Loading…">
        <PageContentBody><div className="loading-state">Loading…</div></PageContentBody>
      </PageContent>
    );
  }
  if (detailsQuery.error) {
    return (
      <PageContent title="Error">
        <PageContentBody>
          <div className="error-banner">Failed to load artist: {(detailsQuery.error as Error).message}</div>
        </PageContentBody>
      </PageContent>
    );
  }

  const details = detailsQuery.data!;
  const { artist } = details;
  const poster = pickPoster(artist);
  const banner = pickBanner(artist);

  return (
    <PageContent title={artist.name}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Search"
            iconName={icons.SEARCH}
            isSpinning={search.isPending}
            onPress={() => search.mutate(id)}
          />
          <PageToolbarSeparator />
          <PageToolbarButton
            label="Edit"
            iconName={icons.EDIT}
            isDisabled
            onPress={() => { /* Phase G: edit modal */ }}
          />
          <PageToolbarButton
            label="Delete"
            iconName={icons.DELETE}
            isDisabled
            onPress={() => { /* Phase G: delete modal */ }}
          />
        </PageToolbarSection>
        <PageToolbarSection alignContent="right">
          <PageToolbarButton
            label="Back"
            iconName={icons.ARROW_LEFT}
            to="/library"
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>
        <div
          className="artist-hero"
          style={banner ? { backgroundImage: `linear-gradient(to bottom, rgba(32,32,32,0.6), var(--pageBackground)), url(${banner.url})` } : undefined}
        >
          <div className="artist-hero-poster">
            {poster
              ? <img src={poster.url} alt={artist.name} />
              : <div className="artist-card-poster-fallback"><FontAwesomeIcon icon={icons.MUSIC} /></div>}
          </div>
          <div className="artist-hero-info">
            <h1 className="artist-hero-title">{artist.name}</h1>
            {artist.disambiguation && <div className="artist-hero-tagline">{artist.disambiguation}</div>}
            <div className="artist-hero-stats">
              {artist.country && <span><strong>{artist.country}</strong></span>}
              <span>{details.videoCount} videos · {details.downloadedCount} downloaded</span>
              <StatusPill variant={artist.monitored ? "monitored" : "unmonitored"}>
                {artist.monitored ? "Monitored" : "Unmonitored"}
              </StatusPill>
              {artist.genres.length > 0 && <span>{artist.genres.join(" · ")}</span>}
            </div>
          </div>
        </div>

        <Card title="Information">
          <dl className="meta">
            <dt>Sort name</dt>
            <dd>{artist.sortName}</dd>
            <dt>Aliases</dt>
            <dd>{details.aliases.length > 0 ? details.aliases.join(", ") : "—"}</dd>
            <dt>Root folder</dt>
            <dd><code>{artist.rootFolderPath}</code></dd>
            <dt>Added</dt>
            <dd>{new Date(artist.added).toLocaleDateString()}</dd>
            <dt>Last metadata sync</dt>
            <dd>{artist.lastInfoSync ? new Date(artist.lastInfoSync).toLocaleString() : "never"}</dd>
          </dl>
        </Card>

        <Card title="YouTube channels">
          <div className="form-row">
            <label htmlFor="yt-channels">
              Comma-separated channel IDs (UC…) — used for RSS sync.
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
            onClick={() => saveChannels.mutate(channelsDraft.split(",").map((c) => c.trim()).filter(Boolean))}
          >
            <FontAwesomeIcon icon={icons.SAVE} />
            Save channels
          </button>
        </Card>

        <Card title={`Music videos (${videosQuery.data?.length ?? 0})`}>
          {videosQuery.isLoading && <div className="loading-state">Loading…</div>}
          {videosQuery.data && videosQuery.data.length === 0 && (
            <EmptyState
              icon={<FontAwesomeIcon icon={icons.MUSIC} />}
              title="No videos yet"
              description="Trigger Search now to pull from configured indexers."
            />
          )}
          {videosQuery.data && videosQuery.data.length > 0 && (
            <table className="grid">
              <thead>
                <tr>
                  <th></th>
                  <th>Title</th>
                  <th>Year</th>
                  <th>Director</th>
                  <th>Runtime</th>
                  <th>Status</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {videosQuery.data.map((v) => (
                  <tr key={v.id}>
                    <td style={{ width: 56 }}>
                      {v.thumbnailUrl ? (
                        <img src={v.thumbnailUrl} alt="" loading="lazy"
                          style={{ width: 56, height: 32, objectFit: "cover", borderRadius: 2 }} />
                      ) : null}
                    </td>
                    <td>
                      <strong>{v.title}</strong>
                      {v.genres.length > 0 && (
                        <div className="muted" style={{ fontSize: "var(--smallFontSize)" }}>
                          {v.genres.slice(0, 3).join(", ")}
                        </div>
                      )}
                    </td>
                    <td>{v.year ?? "—"}</td>
                    <td>{v.director ?? "—"}</td>
                    <td>{formatRuntime(v.runtimeSeconds)}</td>
                    <td>
                      {v.hasFile
                        ? <StatusPill variant="success">Downloaded</StatusPill>
                        : v.monitored
                          ? <StatusPill variant="warning">Wanted</StatusPill>
                          : <StatusPill variant="unmonitored">Ignored</StatusPill>}
                    </td>
                    <td className="actions">
                      <button
                        type="button"
                        onClick={() => {
                          setSearchVideoId(v.id);
                          setSearchResults([]);
                          interactive.mutate({ musicVideoId: v.id });
                        }}
                      >
                        <FontAwesomeIcon icon={icons.SEARCH} />
                        Search
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
                onClick={() => { setSearchVideoId(null); setSearchResults([]); }}
              >
                Close
              </button>
            }
          >
            {interactive.isPending && <div className="loading-state">Searching…</div>}
            {!interactive.isPending && searchResults.length === 0 && (
              <EmptyState
                icon={<FontAwesomeIcon icon={icons.REFRESH} />}
                title="No releases"
                description="No indexer returned a match."
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

        <p style={{ marginTop: 16 }}>
          <Link to="/library">← Back to Library</Link>
        </p>
      </PageContentBody>
    </PageContent>
  );
}
