import { useQuery } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api } from "../api";
import { Card, EmptyState, StatusPill } from "../components/ui";

function formatTime(iso: string): string {
  return new Date(iso).toLocaleString();
}

function eventVariant(t: string) {
  const lower = t.toLowerCase();
  if (lower.includes("grab"))     return "info" as const;
  if (lower.includes("import"))   return "success" as const;
  if (lower.includes("upgrade"))  return "success" as const;
  if (lower.includes("delete"))   return "warning" as const;
  if (lower.includes("fail"))     return "danger" as const;
  return "muted" as const;
}

export function HistoryPage(): JSX.Element {
  const history = useQuery({
    queryKey: ["history"],
    queryFn: () => api.listHistory(),
    refetchInterval: 10_000,
  });

  return (
    <Card title="Event history">
      {history.isLoading && <div className="loading-state">Loading…</div>}
      {history.data && history.data.length === 0 && (
        <EmptyState
          icon={<FontAwesomeIcon icon={icons.CLOCK} />}
          title="No history yet"
          description="Grabs, imports, upgrades, and deletes will show up here."
        />
      )}
      {history.data && history.data.length > 0 && (
        <table className="grid">
          <thead>
            <tr>
              <th>When</th>
              <th>Event</th>
              <th>Title</th>
              <th>Indexer</th>
              <th>Download Client</th>
            </tr>
          </thead>
          <tbody>
            {history.data.map((h) => (
              <tr key={h.id}>
                <td>{formatTime(h.date)}</td>
                <td><StatusPill variant={eventVariant(h.eventType)}>{h.eventType}</StatusPill></td>
                <td>{h.releaseTitle ?? "—"}</td>
                <td>{h.indexerName ?? "—"}</td>
                <td>{h.downloadClientName ?? "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  );
}
