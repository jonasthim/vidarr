import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

function formatTime(iso: string): string {
  return new Date(iso).toLocaleString();
}

export function HistoryPage(): JSX.Element {
  const history = useQuery({
    queryKey: ["history"],
    queryFn: () => api.listHistory(),
    refetchInterval: 10_000,
  });

  return (
    <section className="history">
      <h2>History</h2>
      {history.data?.length === 0 && <p>No history events yet.</p>}
      <table>
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
          {history.data?.map((h) => (
            <tr key={h.id}>
              <td>{formatTime(h.date)}</td>
              <td>{h.eventType}</td>
              <td>{h.releaseTitle ?? "—"}</td>
              <td>{h.indexerName ?? "—"}</td>
              <td>{h.downloadClientName ?? "—"}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
