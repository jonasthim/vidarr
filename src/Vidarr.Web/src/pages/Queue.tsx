import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

function formatBytes(n: number | undefined): string {
  if (n === undefined || n === null) return "?";
  if (n > 1_000_000_000) return `${(n / 1_000_000_000).toFixed(2)} GB`;
  if (n > 1_000_000) return `${(n / 1_000_000).toFixed(1)} MB`;
  return `${n} B`;
}

function percent(total: number | undefined, remaining: number | undefined): string {
  if (total === undefined || total === null || total === 0) return "—";
  if (remaining === undefined || remaining === null) return "—";
  const done = ((total - remaining) / total) * 100;
  return `${done.toFixed(0)}%`;
}

export function QueuePage(): JSX.Element {
  const queue = useQuery({
    queryKey: ["queue"],
    queryFn: api.listQueue,
    refetchInterval: 2000,
  });

  return (
    <section className="queue">
      <h2>Queue</h2>
      {queue.data?.length === 0 && <p>Nothing in flight.</p>}
      <table>
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
              <td>{q.status}</td>
              <td>{percent(q.totalBytes, q.remainingBytes)}</td>
              <td>{formatBytes(q.totalBytes)}</td>
              <td>
                {q.etaSeconds !== undefined && q.etaSeconds !== null
                  ? `${Math.round(q.etaSeconds)}s`
                  : "—"}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
