import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

function formatInterval(seconds: number): string {
  if (seconds >= 86400) return `${Math.round(seconds / 86400)}d`;
  if (seconds >= 3600) return `${Math.round(seconds / 3600)}h`;
  if (seconds >= 60) return `${Math.round(seconds / 60)}m`;
  return `${seconds}s`;
}

function formatTime(iso?: string): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleString();
}

export function CommandsPage(): JSX.Element {
  const queryClient = useQueryClient();
  const commands = useQuery({
    queryKey: ["systemCommands"],
    queryFn: api.listCommands,
    refetchInterval: 5000,
  });

  const run = useMutation({
    mutationFn: api.triggerCommand,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["systemCommands"] }),
  });

  return (
    <section className="commands">
      <h2>Commands</h2>
      <p className="muted">
        Recurring jobs run on their own schedule; click "Run now" to trigger one
        immediately. Last-run state refreshes every 5 seconds.
      </p>
      <table>
        <thead>
          <tr>
            <th>Job</th>
            <th>Interval</th>
            <th>Last run</th>
            <th>Status</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {commands.data?.map((c) => (
            <tr key={c.name}>
              <td>
                <strong>{c.name}</strong>
              </td>
              <td>{formatInterval(c.intervalSeconds)}</td>
              <td>{formatTime(c.lastRun)}</td>
              <td>
                {c.lastRunOk ? (
                  <span className="muted">✓ ok</span>
                ) : (
                  <span className="muted">—</span>
                )}
                {c.recent[0]?.failureReason && (
                  <div className="error">{c.recent[0].failureReason}</div>
                )}
              </td>
              <td>
                <button
                  type="button"
                  disabled={run.isPending}
                  onClick={() => run.mutate(c.name)}
                >
                  Run now
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}
