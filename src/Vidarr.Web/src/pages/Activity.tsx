import { Navigate, useParams } from "react-router-dom";
import { useQueryClient } from "@tanstack/react-query";
import { icons } from "../Components/Icon/Icon";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
import { QueuePage } from "./Queue";
import { HistoryPage } from "./History";
import { BlocklistPanel } from "../components/BlocklistPanel";

const TABS: Record<string, { label: string; queryKey: string; render: () => JSX.Element }> = {
  queue:     { label: "Queue",     queryKey: "queue",     render: () => <QueuePage /> },
  history:   { label: "History",   queryKey: "history",   render: () => <HistoryPage /> },
  blocklist: { label: "Blocklist", queryKey: "blocklist", render: () => <BlocklistPanel /> },
};

export function ActivityPage(): JSX.Element {
  const { tab } = useParams<{ tab: string }>();
  const queryClient = useQueryClient();
  if (!tab || !TABS[tab]) return <Navigate to="/activity/queue" replace />;
  const active = TABS[tab];
  return (
    <PageContent title={active.label}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Refresh"
            iconName={icons.REFRESH}
            onPress={() => queryClient.invalidateQueries({ queryKey: [active.queryKey] })}
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>
        {active.render()}
      </PageContentBody>
    </PageContent>
  );
}
