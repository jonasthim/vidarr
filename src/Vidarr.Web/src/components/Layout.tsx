import { Outlet } from "react-router-dom";
import { Page, PageMain } from "../Components/Page/Page";
import { PageHeader } from "../Components/Page/Header/PageHeader";
import { PageSidebar } from "../Components/Page/Sidebar/PageSidebar";
import { PageContent } from "../Components/Page/PageContent";

export function Layout(): JSX.Element {
  return (
    <Page>
      <PageHeader />
      <PageMain>
        <PageSidebar />
        <PageContent>
          <Outlet />
        </PageContent>
      </PageMain>
    </Page>
  );
}
