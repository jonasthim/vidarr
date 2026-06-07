import { Navigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { icons } from "../Components/Icon/Icon";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
import { Card, StatusPill } from "../components/ui";
import { ComingSoonPanel } from "../components/ComingSoonPanel";
import { CommandsPage } from "./Commands";
import { HealthPage } from "./Health";
import { BackupPanel } from "../components/BackupPanel";
import { api } from "../api";

const UpdatesStub = () => (
  <ComingSoonPanel
    title="Updates"
    description="Installed Vidarr version + a list of available release notes pulled from GitHub. Sonarr lets you trigger an in-place upgrade from this page."
    needs={<span>a release-feed endpoint (<code>/api/v1/update</code>) + the auto-update flag on <code>ApplicationConfig</code> + a small UI.</span>}
  />
);
const EventsStub = () => (
  <ComingSoonPanel
    title="Events"
    description="A pageable view of recent in-process EventBus traffic — artist refreshes, RSS syncs, decision rejections, etc."
    needs={<span>a ring-buffer subscription on top of <code>IEventBus</code> + a new<code> /api/v1/system/events</code> stream endpoint.</span>}
  />
);
const LogsStub = () => (
  <ComingSoonPanel
    title="Log Files"
    description="Browse + download the Serilog rolling files written under data/logs."
    needs={<span>a <code>/api/v1/system/log</code> endpoint that lists / serves files from the log directory.</span>}
  />
);

const TABS: Record<string, { label: string; render: () => JSX.Element }> = {
  status:  { label: "Status",    render: () => <StatusTab /> },
  tasks:   { label: "Tasks",     render: () => <CommandsPage /> },
  health:  { label: "Health",    render: () => <HealthPage /> },
  backup:  { label: "Backup",    render: () => <BackupPanel /> },
  updates: { label: "Updates",   render: () => <UpdatesStub /> },
  events:  { label: "Events",    render: () => <EventsStub /> },
  logs:    { label: "Log Files", render: () => <LogsStub /> },
};

export function SystemPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  const queryClient = useQueryClient();
  if (!tab || !TABS[tab]) return <Navigate to="/system/status" replace />;
  const active = TABS[tab];
  return (
    <PageContent title={active.label}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Refresh"
            iconName={icons.REFRESH}
            onPress={() => queryClient.invalidateQueries()}
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>{active.render()}</PageContentBody>
    </PageContent>
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
