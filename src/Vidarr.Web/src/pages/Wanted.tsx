import { Navigate, useParams, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, type MusicVideoListItem } from "../api";
import { PageHeader, Card, EmptyState, StatusPill } from "../components/ui";

const TABS: Record<string, { label: string; query: keyof typeof TAB_QUERIES }> = {
  missing: { label: "Missing",      query: "missing" },
  cutoff:  { label: "Cutoff Unmet", query: "cutoff" },
};

const TAB_QUERIES = {
  missing: { qfn: api.listMissing,      key: "wanted-missing" as const, emptyMessage: "Nothing missing — all monitored videos have files." },
  cutoff:  { qfn: api.listCutoffUnmet,  key: "wanted-cutoff" as const,  emptyMessage: "No cutoff-unmet videos — every download meets its profile target." },
};

export function WantedPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  if (!tab || !TABS[tab]) return <Navigate to="/wanted/missing" replace />;
  const active = TABS[tab];
  const q = TAB_QUERIES[active.query];
  const { data, isLoading, error } = useQuery({
    queryKey: [q.key],
    queryFn: q.qfn,
    refetchInterval: 30_000,
  });
  const description = tab === "cutoff"
    ? "Videos that have a file, but the file's quality is below the artist's profile cutoff and would be upgraded if a better release is found."
    : "Monitored music videos that don't yet have a file on disk.";
  return (
    <>
      <PageHeader title={active.label} subtitle="Wanted" />
      <Card title={active.label}>
        <p className="muted">{description}</p>
        {renderTable(data, isLoading, error, q.emptyMessage)}
      </Card>
    </>
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
                <FontAwesomeIcon icon={icons.MUSIC} />
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
