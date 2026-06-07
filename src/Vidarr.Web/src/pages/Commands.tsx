import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api } from "../api";
import { StatusPill } from "../components/ui";

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
    <table className="grid">
      <thead>
        <tr>
          <th>Job</th>
          <th>Interval</th>
          <th>Last run</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        {commands.data?.map((c) => (
          <tr key={c.name}>
            <td><strong>{c.name}</strong></td>
            <td>{formatInterval(c.intervalSeconds)}</td>
            <td>{formatTime(c.lastRun)}</td>
            <td>
              {c.lastRun
                ? (c.lastRunOk
                    ? <StatusPill variant="success">OK</StatusPill>
                    : <StatusPill variant="danger">Failed</StatusPill>)
                : <StatusPill variant="muted">Not yet run</StatusPill>}
              {c.recent[0]?.failureReason && (
                <div className="error-banner" style={{ marginTop: 8 }}>
                  {c.recent[0].failureReason}
                </div>
              )}
            </td>
            <td className="actions">
              <button type="button" disabled={run.isPending} onClick={() => run.mutate(c.name)}>
                <FontAwesomeIcon icon={icons.PLAY} />
                Run now
              </button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
