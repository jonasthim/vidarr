import { Link, useParams } from "react-router-dom";
import { PageHeader, EmptyState } from "../components/ui";
import { Music } from "lucide-react";

export function ArtistDetailStub(): JSX.Element {
  const { artistId } = useParams<{ artistId: string }>();
  return (
    <>
      <PageHeader title={`Artist #${artistId}`} subtitle="Detail view coming soon" />
      <EmptyState
        icon={<Music />}
        title="Artist detail view coming in P2"
        description="Posters, full metadata, monitored toggle and per-video actions. Today, use the Library page list view."
        action={<Link to="/library" className="btn">Back to Library</Link>}
      />
    </>
  );
}
