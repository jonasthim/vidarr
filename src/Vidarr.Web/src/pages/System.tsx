import { Navigate, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { PageHeader, Card, StatusPill } from "../components/ui";
import { CommandsPage } from "./Commands";
import { HealthPage } from "./Health";
import { BackupPanel } from "../components/BackupPanel";
import { api } from "../api";

const TABS: Record<string, { label: string; render: () => JSX.Element }> = {
  status: { label: "Status", render: () => <StatusTab /> },
  tasks:  { label: "Tasks",  render: () => <CommandsPage /> },
  health: { label: "Health", render: () => <HealthPage /> },
  backup: { label: "Backup", render: () => <BackupPanel /> },
};

export function SystemPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  if (!tab || !TABS[tab]) return <Navigate to="/system/status" replace />;
  const active = TABS[tab];
  return (
    <>
      <PageHeader title={active.label} subtitle="System" />
      {active.render()}
    </>
  );
}

function StatusTab(): JSX.Element {
  const statusQuery = useQuery({
    queryKey: ["system-status"],
    queryFn: api.getSystemStatus,
    refetchInterval: false,
  });
  return (
    <Card title="Instance">
      {statusQuery.isLoading && <div className="loading-state">Loading…</div>}
      {statusQuery.error && <div className="error-banner">Failed: {(statusQuery.error as Error).message}</div>}
      {statusQuery.data && (
        <dl className="meta">
          <dt>Version</dt>
          <dd>{statusQuery.data.version}</dd>
          <dt>Server time</dt>
          <dd>{new Date(statusQuery.data.buildtime).toLocaleString()}</dd>
          <dt>Authenticated</dt>
          <dd>
            {statusQuery.data.authenticated
              ? <StatusPill variant="success">Yes</StatusPill>
              : <StatusPill variant="warning">No</StatusPill>}
          </dd>
        </dl>
      )}
    </Card>
  );
}
