import { AlertCircle } from "lucide-react";
import { PageHeader, EmptyState } from "../components/ui";

export function WantedPlaceholder(): JSX.Element {
  return (
    <>
      <PageHeader title="Wanted" subtitle="Missing and cutoff-unmet music videos" />
      <EmptyState
        icon={<AlertCircle />}
        title="Wanted view coming soon"
        description="Phase 3 of the UI roadmap. Needs server-side filters for monitored / hasFile / cutoffUnmet on /api/v1/musicvideo."
      />
    </>
  );
}
