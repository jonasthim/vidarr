import { Navigate, useParams } from "react-router-dom";
import { PageHeader } from "../components/ui";
import { ProfilesPanel } from "../components/ProfilesPanel";
import { RootFoldersPanel } from "../components/RootFoldersPanel";
import { TagsPanel } from "../components/TagsPanel";
import { GeneralSettingsPanel } from "../components/GeneralSettingsPanel";
import { IndexersPanel } from "../components/IndexersPanel";
import { DownloadClientsPanel } from "../components/DownloadClientsPanel";
import { CustomFormatsPanel } from "../components/CustomFormatsPanel";
import { BlocklistPanel } from "../components/BlocklistPanel";
import { DiscoveryRulesPanel } from "../components/DiscoveryRulesPanel";
import { NotificationsPanel } from "../components/NotificationsPanel";
import { SecurityPanel } from "../components/SecurityPanel";

type SectionDef = {
  slug: string;
  label: string;
  panel: () => JSX.Element;
};

const SECTIONS: SectionDef[] = [
  { slug: "mediamanagement", label: "Media Management", panel: GeneralSettingsPanel },
  { slug: "profiles",        label: "Profiles",         panel: ProfilesPanel },
  { slug: "quality",         label: "Custom Formats",   panel: CustomFormatsPanel },
  { slug: "indexers",        label: "Indexers",         panel: IndexersPanel },
  { slug: "downloadclients", label: "Download Clients", panel: DownloadClientsPanel },
  { slug: "importlists",     label: "Discovery Rules",  panel: DiscoveryRulesPanel },
  { slug: "connect",         label: "Connect",          panel: NotificationsPanel },
  { slug: "tags",            label: "Tags",             panel: TagsPanel },
  { slug: "rootfolders",     label: "Root Folders",     panel: RootFoldersPanel },
  { slug: "blocklist",       label: "Blocklist",        panel: BlocklistPanel },
  { slug: "security",        label: "Security",         panel: SecurityPanel },
];

export function SettingsPage(): JSX.Element {
  const { section } = useParams<{ section: string }>();
  const active = SECTIONS.find((s) => s.slug === section);
  if (!active) return <Navigate to="/settings/mediamanagement" replace />;
  const Panel = active.panel;
  return (
    <>
      <PageHeader title={active.label} subtitle="Settings" />
      <Panel />
    </>
  );
}
