import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { api, HealthIssue } from "../api";
import { EmptyState, StatusPill } from "../components/ui";

function severityVariant(s: string) {
  switch (s) {
    case "Error":   return "danger" as const;
    case "Warning": return "warning" as const;
    default:        return "info" as const;
  }
}

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

  if (isLoading) return <div className="loading-state">Loading…</div>;
  if (error) return <div className="error-banner">Failed to load: {(error as Error).message}</div>;
  return (
    <>
      <p className="muted" style={{ marginBottom: 12 }}>
        Last run: {data?.lastRun ? new Date(data.lastRun).toLocaleString() : "never"}{" "}
        <button type="button" disabled={runMutation.isPending} onClick={() => runMutation.mutate()}>
          <FontAwesomeIcon icon={icons.REFRESH} />
          {runMutation.isPending ? "Running…" : "Run checks now"}
        </button>
      </p>
      {data && data.issues.length === 0 ? (
        <EmptyState
          icon={<FontAwesomeIcon icon={icons.HEALTH} />}
          title="All checks passing"
          description="No active issues. Vidarr re-runs health checks every 15 minutes."
        />
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
            {data?.issues.map((issue, i) => (
              <tr key={`${issue.checkName}-${issue.source}-${i}`}>
                <td><StatusPill variant={severityVariant(issue.severity)}>{issue.severity}</StatusPill></td>
                <td>{issue.checkName}</td>
                <td>{issue.source}</td>
                <td>{issue.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}

export function severityRank(issue: HealthIssue): number {
  switch (issue.severity) {
    case "Error":   return 0;
    case "Warning": return 1;
    default:        return 2;
  }
}
