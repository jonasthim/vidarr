import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, type ArtistDto } from "../api";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
import { PageToolbarSeparator } from "../Components/Page/Toolbar/PageToolbarSeparator";
import { EmptyState, StatusPill } from "../components/ui";
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
    <PageContent title="Library">
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Posters"
            iconName={icons.POSTERS}
            isActive={view === "poster"}
            onPress={() => setView("poster")}
          />
          <PageToolbarButton
            label="Banners"
            iconName={icons.TABLE}
            isActive={view === "banner"}
            onPress={() => setView("banner")}
          />
          <PageToolbarButton
            label="Table"
            iconName={icons.LIST}
            isActive={view === "table"}
            onPress={() => setView("table")}
          />
          <PageToolbarSeparator />
          <PageToolbarButton
            label="Refresh"
            iconName={icons.REFRESH}
            isSpinning={refresh.isPending}
            onPress={() => refresh.mutate()}
          />
        </PageToolbarSection>
        <PageToolbarSection alignContent="right">
          <PageToolbarButton
            label="Add Artist"
            iconName={icons.ADD}
            to="/add"
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>
        <div className="library-toolbar">
          <FontAwesomeIcon icon={icons.SEARCH} className="muted" />
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
            icon={<FontAwesomeIcon icon={icons.MUSIC} />}
            title="No artists yet"
            description="Use Add Artist to start your library."
          />
        )}
        {artists.data && artists.data.length > 0 && filtered.length === 0 && (
          <EmptyState
            icon={<FontAwesomeIcon icon={icons.MUSIC} />}
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
                      <div className="muted" style={{ fontSize: "var(--smallFontSize)" }}>
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
      </PageContentBody>
    </PageContent>
  );
}
