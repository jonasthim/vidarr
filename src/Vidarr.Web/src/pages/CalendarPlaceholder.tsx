import { Calendar } from "lucide-react";
import { PageHeader, EmptyState } from "../components/ui";

export function CalendarPlaceholder(): JSX.Element {
  return (
    <>
      <PageHeader title="Calendar" subtitle="Premiere dates for monitored videos" />
      <EmptyState
        icon={<Calendar />}
        title="Calendar view coming soon"
        description="Phase 3 of the UI roadmap. Needs a /api/v1/calendar endpoint and premiere-date enrichment on music videos."
      />
    </>
  );
}
