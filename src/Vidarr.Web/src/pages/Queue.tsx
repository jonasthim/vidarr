import { useQuery } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api } from "../api";
import { EmptyState, StatusPill } from "../components/ui";

function formatBytes(n: number | undefined | null): string {
  if (n === undefined || n === null) return "—";
  if (n > 1_000_000_000) return `${(n / 1_000_000_000).toFixed(2)} GB`;
  if (n > 1_000_000) return `${(n / 1_000_000).toFixed(1)} MB`;
  return `${n} B`;
}

function percent(total: number | undefined | null, remaining: number | undefined | null): string {
  if (!total) return "—";
  if (remaining === undefined || remaining === null) return "—";
  const done = ((total - remaining) / total) * 100;
  return `${done.toFixed(0)}%`;
}

function statusVariant(s: string) {
  const lower = s.toLowerCase();
  if (lower.includes("complete") || lower.includes("done")) return "success" as const;
  if (lower.includes("warn"))    return "warning" as const;
  if (lower.includes("error") || lower.includes("fail")) return "danger" as const;
  return "info" as const;
}

export function QueuePage(): JSX.Element {
  const queue = useQuery({
    queryKey: ["queue"],
    queryFn: api.listQueue,
    refetchInterval: 2000,
  });

  if (queue.isLoading) return <div className="loading-state">Loading…</div>;
  if (queue.data && queue.data.length === 0) {
    return (
      <EmptyState
        icon={<FontAwesomeIcon icon={icons.DOWNLOADING} />}
        title="Nothing in flight"
        description="Grabbed releases show up here while downloading."
      />
    );
  }
  return (
    <table className="grid">
      <thead>
        <tr>
          <th>Title</th>
          <th>Status</th>
          <th>Progress</th>
          <th>Size</th>
          <th>ETA</th>
        </tr>
      </thead>
      <tbody>
        {queue.data?.map((q) => (
          <tr key={q.id}>
            <td>{q.title}</td>
            <td><StatusPill variant={statusVariant(q.status)}>{q.status}</StatusPill></td>
            <td>{percent(q.totalBytes, q.remainingBytes)}</td>
            <td>{formatBytes(q.totalBytes)}</td>
            <td>{q.etaSeconds !== undefined && q.etaSeconds !== null ? `${Math.round(q.etaSeconds)}s` : "—"}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
