import { Navigate, useParams } from "react-router-dom";
import { PageHeader, Tabs } from "../components/ui";
import { QueuePage } from "./Queue";
import { HistoryPage } from "./History";
import { BlocklistPanel } from "../components/BlocklistPanel";

const TABS = [
  { to: "/activity/queue",     label: "Queue" },
  { to: "/activity/history",   label: "History" },
  { to: "/activity/blocklist", label: "Blocklist" },
];

export function ActivityPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  if (!tab) return <Navigate to="/activity/queue" replace />;

  return (
    <>
      <PageHeader title="Activity" />
      <Tabs tabs={TABS} />
      {tab === "queue" && <QueuePage />}
      {tab === "history" && <HistoryPage />}
      {tab === "blocklist" && <BlocklistPanel />}
    </>
  );
}
