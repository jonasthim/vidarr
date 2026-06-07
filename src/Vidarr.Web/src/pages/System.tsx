import { Navigate, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { PageHeader, Tabs, Card, StatusPill } from "../components/ui";
import { CommandsPage } from "./Commands";
import { HealthPage } from "./Health";
import { BackupPanel } from "../components/BackupPanel";
import { api } from "../api";

const TABS = [
  { to: "/system/status", label: "Status" },
  { to: "/system/tasks",  label: "Tasks" },
  { to: "/system/health", label: "Health" },
  { to: "/system/backup", label: "Backup" },
];

export function SystemPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  if (!tab) return <Navigate to="/system/status" replace />;

  return (
    <>
      <PageHeader title="System" />
      <Tabs tabs={TABS} />
      {tab === "status" && <StatusTab />}
      {tab === "tasks" && <CommandsPage />}
      {tab === "health" && <HealthPage />}
      {tab === "backup" && <BackupPanel />}
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
