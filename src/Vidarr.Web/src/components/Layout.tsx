import { Outlet } from "react-router-dom";
import { Page, PageMain } from "../Components/Page/Page";
import { PageHeader } from "../Components/Page/Header/PageHeader";
import { PageSidebar } from "../Components/Page/Sidebar/PageSidebar";

/*
 * Sonarr structure: <Page><PageHeader/><PageMain><PageSidebar/><Outlet/></PageMain></Page>.
 * Each page emits its own <PageContent><PageToolbar/><PageContentBody/></PageContent>.
 */
export function Layout(): JSX.Element {
  return (
    <Page>
      <PageHeader />
      <PageMain>
        <PageSidebar />
        <Outlet />
      </PageMain>
    </Page>
  );
}
