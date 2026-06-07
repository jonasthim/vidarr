import { Navigate, useParams } from "react-router-dom";
import { PageHeader } from "../components/ui";
import { QueuePage } from "./Queue";
import { HistoryPage } from "./History";
import { BlocklistPanel } from "../components/BlocklistPanel";

const TABS: Record<string, { label: string; render: () => JSX.Element }> = {
  queue:     { label: "Queue",     render: () => <QueuePage /> },
  history:   { label: "History",   render: () => <HistoryPage /> },
  blocklist: { label: "Blocklist", render: () => <BlocklistPanel /> },
};

export function ActivityPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  if (!tab || !TABS[tab]) return <Navigate to="/activity/queue" replace />;
  const active = TABS[tab];
  return (
    <>
      <PageHeader title={active.label} subtitle="Activity" />
      {active.render()}
    </>
  );
}
