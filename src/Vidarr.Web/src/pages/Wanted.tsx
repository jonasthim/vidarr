import { Navigate, useParams, Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, type MusicVideoListItem } from "../api";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
import { EmptyState, StatusPill } from "../components/ui";

const TABS: Record<string, { label: string; queryKey: string; emptyMessage: string }> = {
  missing: { label: "Missing", queryKey: "wanted-missing", emptyMessage: "Nothing missing — all monitored videos have files." },
  cutoff:  { label: "Cutoff Unmet", queryKey: "wanted-cutoff", emptyMessage: "No cutoff-unmet videos — every download meets its profile target." },
};

const FETCHERS = {
  missing: api.listMissing,
  cutoff:  api.listCutoffUnmet,
};

export function WantedPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  const queryClient = useQueryClient();
  if (!tab || !TABS[tab]) return <Navigate to="/wanted/missing" replace />;
  const active = TABS[tab];
  const fetcher = FETCHERS[tab as keyof typeof FETCHERS];
  const { data, isLoading, error } = useQuery({
    queryKey: [active.queryKey],
    queryFn: fetcher,
    refetchInterval: 30_000,
  });
  return (
    <PageContent title={active.label}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Refresh"
            iconName={icons.REFRESH}
            onPress={() => queryClient.invalidateQueries({ queryKey: [active.queryKey] })}
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>
        {isLoading && <div className="loading-state">Loading…</div>}
        {error && <div className="error-banner">Failed: {(error as Error).message}</div>}
        {data && data.length === 0 && (
          <EmptyState
            icon={<FontAwesomeIcon icon={icons.ALERT} />}
            title="All caught up"
            description={active.emptyMessage}
          />
        )}
        {data && data.length > 0 && (
          <table className="grid">
            <thead>
              <tr>
                <th></th>
                <th>Artist</th>
                <th>Title</th>
                <th>Year</th>
                <th>Type</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {data.map((v: MusicVideoListItem) => (
                <tr key={v.id}>
                  <td style={{ width: 56 }}>
                    {v.thumbnailUrl ? (
                      <img src={v.thumbnailUrl} alt="" loading="lazy"
                        style={{ width: 56, height: 32, objectFit: "cover", borderRadius: 2 }} />
                    ) : (
                      <FontAwesomeIcon icon={icons.MUSIC} />
                    )}
                  </td>
                  <td><Link to={`/library/${v.artistId}`}>{v.artistName}</Link></td>
                  <td>{v.title}</td>
                  <td>{v.year ?? "—"}</td>
                  <td>{v.type}</td>
                  <td>
                    {v.hasFile
                      ? <StatusPill variant="warning">Below cutoff</StatusPill>
                      : <StatusPill variant="warning">Wanted</StatusPill>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </PageContentBody>
    </PageContent>
  );
}
