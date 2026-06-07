import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import {
  Grid3x3,
  Image,
  LayoutList,
  Music,
  Search,
} from "lucide-react";
import { api, type ArtistDto } from "../api";
import { PageHeader, EmptyState, StatusPill } from "../components/ui";
import { ArtistCard } from "../components/ArtistCard";
import { ArtistBanner } from "../components/ArtistBanner";

type LibraryView = "poster" | "banner" | "table";
const STORAGE_KEY = "vidarr.libraryView";

function readStoredView(): LibraryView {
  const v = window.localStorage.getItem(STORAGE_KEY);
  return v === "banner" || v === "table" ? v : "poster";
}

export function LibraryPage(): JSX.Element {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState("");
  const [view, setView] = useState<LibraryView>(() => readStoredView());

  useEffect(() => {
    window.localStorage.setItem(STORAGE_KEY, view);
  }, [view]);

  const artists = useQuery({
    queryKey: ["artists"],
    queryFn: api.listArtists,
  });

  const refresh = useMutation({
    mutationFn: api.listArtists,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["artists"] }),
  });

  const filtered = useMemo<ArtistDto[]>(() => {
    if (!artists.data) return [];
    if (!filter) return artists.data;
    const lower = filter.toLowerCase();
    return artists.data.filter(
      (a) =>
        a.name.toLowerCase().includes(lower) ||
        (a.disambiguation?.toLowerCase().includes(lower) ?? false),
    );
  }, [artists.data, filter]);

  return (
    <>
      <PageHeader
        title="Library"
        subtitle={`${artists.data?.length ?? 0} artist${artists.data?.length === 1 ? "" : "s"} monitored`}
        actions={
          <>
            <ViewToggle view={view} onChange={setView} />
            <button
              type="button"
              onClick={() => refresh.mutate()}
              disabled={refresh.isPending}
            >
              <Search size={14} />
              Refresh
            </button>
          </>
        }
      />

      <div className="library-toolbar">
        <Search size={14} className="muted" />
        <input
          type="search"
          placeholder="Filter library…"
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
      {artists.data && artists.data.length > 0 && filtered.length === 0 && (
        <EmptyState
          icon={<Music />}
          title="No matches"
          description={`Nothing matches "${filter}".`}
        />
      )}

      {view === "poster" && filtered.length > 0 && (
        <div className="poster-grid">
          {filtered.map((a) => (
            <ArtistCard key={a.id} artist={a} />
          ))}
        </div>
      )}
      {view === "banner" && filtered.length > 0 && (
        <div className="banner-grid">
          {filtered.map((a) => (
            <ArtistBanner key={a.id} artist={a} />
          ))}
        </div>
      )}
      {view === "table" && filtered.length > 0 && (
        <table className="grid">
          <thead>
            <tr>
              <th>Name</th>
              <th>Country</th>
              <th>Genres</th>
              <th>Monitored</th>
              <th>Added</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((a) => (
              <tr key={a.id}>
                <td>
                  <Link to={`/library/${a.id}`}>{a.name}</Link>
                  {a.disambiguation && (
                    <div className="muted" style={{ fontSize: "var(--fs-sm)" }}>
                      {a.disambiguation}
                    </div>
                  )}
                </td>
                <td>{a.country ?? "—"}</td>
                <td>{a.genres.length > 0 ? a.genres.slice(0, 3).join(", ") : "—"}</td>
                <td>
                  <StatusPill variant={a.monitored ? "monitored" : "unmonitored"}>
                    {a.monitored ? "Yes" : "No"}
                  </StatusPill>
                </td>
                <td>{new Date(a.added).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

function ViewToggle({
  view,
  onChange,
}: {
  view: LibraryView;
  onChange: (v: LibraryView) => void;
}): JSX.Element {
  const options: { v: LibraryView; icon: JSX.Element; label: string }[] = [
    { v: "poster", icon: <Grid3x3 size={14} />, label: "Posters" },
    { v: "banner", icon: <Image size={14} />, label: "Banners" },
    { v: "table",  icon: <LayoutList size={14} />, label: "Table" },
  ];
  return (
    <div className="view-toggle">
      {options.map((o) => (
        <button
          key={o.v}
          type="button"
          className={view === o.v ? "primary" : ""}
          title={o.label}
          onClick={() => onChange(o.v)}
        >
          {o.icon}
        </button>
      ))}
    </div>
  );
}
