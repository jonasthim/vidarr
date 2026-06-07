import { Navigate, useParams, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, type MusicVideoListItem } from "../api";
import { PageHeader, Tabs, Card, EmptyState, StatusPill } from "../components/ui";

const TABS = [
  { to: "/wanted/missing", label: "Missing" },
  { to: "/wanted/cutoff",  label: "Cutoff Unmet" },
];

export function WantedPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  if (!tab) return <Navigate to="/wanted/missing" replace />;

  return (
    <>
      <PageHeader title="Wanted" />
      <Tabs tabs={TABS} />
      {tab === "missing" && <MissingTab />}
      {tab === "cutoff"  && <CutoffTab />}
    </>
  );
}

function MissingTab(): JSX.Element {
  const { data, isLoading, error } = useQuery({
    queryKey: ["wanted-missing"],
    queryFn: api.listMissing,
    refetchInterval: 30_000,
  });
  return (
    <Card title="Missing">
      <p className="muted">Monitored music videos that don't yet have a file on disk.</p>
      {renderTable(data, isLoading, error, "Nothing missing — all monitored videos have files.")}
    </Card>
  );
}

function CutoffTab(): JSX.Element {
  const { data, isLoading, error } = useQuery({
    queryKey: ["wanted-cutoff"],
    queryFn: api.listCutoffUnmet,
    refetchInterval: 30_000,
  });
  return (
    <Card title="Cutoff Unmet">
      <p className="muted">
        Videos that have a file, but the file's quality is below the artist's profile cutoff
        and would be upgraded if a better release is found.
      </p>
      {renderTable(data, isLoading, error, "No cutoff-unmet videos — every download meets its profile target.")}
    </Card>
  );
}

function renderTable(
  data: MusicVideoListItem[] | undefined,
  isLoading: boolean,
  error: unknown,
  emptyMessage: string,
): JSX.Element {
  if (isLoading) return <div className="loading-state">Loading…</div>;
  if (error) return <div className="error-banner">Failed: {(error as Error).message}</div>;
  if (!data || data.length === 0) {
    return (
      <EmptyState
        icon={<FontAwesomeIcon icon={icons.ALERT} />}
        title="All caught up"
        description={emptyMessage}
      />
    );
  }
  return (
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
        {data.map((v) => (
          <tr key={v.id}>
            <td style={{ width: 56 }}>
              {v.thumbnailUrl ? (
                <img
                  src={v.thumbnailUrl}
                  alt=""
                  loading="lazy"
                  style={{ width: 56, height: 32, objectFit: "cover", borderRadius: 2 }}
                />
              ) : (
                <div className="muted" style={{ display: "flex", alignItems: "center", justifyContent: "center", width: 56, height: 32, background: "var(--bg-panel-alt)", borderRadius: 2 }}>
                  <FontAwesomeIcon icon={icons.MUSIC} />
                </div>
              )}
            </td>
            <td>
              <Link to={`/library/${v.artistId}`}>{v.artistName}</Link>
            </td>
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
  );
}
