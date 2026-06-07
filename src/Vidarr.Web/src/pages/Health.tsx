import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, HealthIssue } from "../api";

export function HealthPage(): JSX.Element {
  const queryClient = useQueryClient();
  const { data, isLoading, error } = useQuery({
    queryKey: ["health"],
    queryFn: api.getHealth,
    refetchInterval: 60_000,
  });
  const runMutation = useMutation({
    mutationFn: api.runHealth,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["health"] }),
  });

  if (isLoading) return <p>Loading health…</p>;
  if (error) return <p className="error">Failed to load health: {(error as Error).message}</p>;
  const status = data!;

  return (
    <section>
      <div className="page-header">
        <h2>Health</h2>
        <button type="button" disabled={runMutation.isPending} onClick={() => runMutation.mutate()}>
          {runMutation.isPending ? "Running…" : "Run checks now"}
        </button>
      </div>
      <p className="muted">
        Last run: {status.lastRun ? new Date(status.lastRun).toLocaleString() : "never"}
      </p>
      {status.issues.length === 0 ? (
        <p className="ok">No active issues. Everything looks healthy.</p>
      ) : (
        <table className="grid">
          <thead>
            <tr>
              <th>Severity</th>
              <th>Check</th>
              <th>Source</th>
              <th>Message</th>
            </tr>
          </thead>
          <tbody>
            {status.issues.map((issue, i) => (
              <tr key={`${issue.checkName}-${issue.source}-${i}`}>
                <td>
                  <span className={`badge severity-${issue.severity.toLowerCase()}`}>
                    {issue.severity}
                  </span>
                </td>
                <td>{issue.checkName}</td>
                <td>{issue.source}</td>
                <td>{issue.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}

export function severityRank(issue: HealthIssue): number {
  switch (issue.severity) {
    case "Error":
      return 0;
    case "Warning":
      return 1;
    default:
      return 2;
  }
}
