import { Navigate, useParams } from "react-router-dom";
import { icons } from "../Components/Icon/Icon";
import { PageContent } from "../Components/Page/PageContent";
import { PageContentBody } from "../Components/Page/PageContentBody";
import { PageToolbar } from "../Components/Page/Toolbar/PageToolbar";
import { PageToolbarSection } from "../Components/Page/Toolbar/PageToolbarSection";
import { PageToolbarButton } from "../Components/Page/Toolbar/PageToolbarButton";
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
import { ComingSoonPanel } from "../components/ComingSoonPanel";

type SectionDef = { slug: string; label: string; panel: () => JSX.Element };

const QualityStub = () => (
  <ComingSoonPanel
    title="Quality"
    description="Per-quality min/max file-size constraints. Sonarr ships this as a list of every Quality definition (Bluray-1080p, WEBDL-720p, …) with editable size guardrails the Decision engine respects when scoring releases."
    needs={<span>a UI for <code>GET</code> / <code>PUT</code> <code>/api/v1/qualitydefinition</code> (backend already exposes the list).</span>}
  />
);
const MetadataStub = () => (
  <ComingSoonPanel
    title="Metadata"
    description="Per-consumer metadata writers (Plex Music Videos / Kodi / Roksbox NFO) that sidecar the music-video files with .nfo + poster/banner companion files."
    needs={<span>a metadata-consumer registry + new <code>/api/v1/metadata</code> CRUD endpoints.</span>}
  />
);
const MetadataSourceStub = () => (
  <ComingSoonPanel
    title="Metadata Source"
    description="Pluggable metadata provider chooser. Vidarr ships with IMVDb today; this page would let you pick alternates (MusicBrainz video link, TheAudioDB, …) once their providers exist."
    needs={<span>provider implementations of <code>IMetadataProvider</code> beyond the existing IMVDb one, plus a UI to configure the active source.</span>}
  />
);
const GeneralStub = () => (
  <ComingSoonPanel
    title="General"
    description="Host port + URL base + branch + analytics + auto-update toggles. Sonarr separates these from Media Management (which only handles file naming + root folders); we keep them combined under Media Management today."
    needs={<span>a split of the existing <code>GeneralSettingsPanel</code> into "Media Management" (naming + ops) and "General" (host + URL base + log level).</span>}
  />
);
const UiStub = () => (
  <ComingSoonPanel
    title="UI"
    description="Date / time format, first day of week, theme picker, colour-impaired mode. Sonarr stores these on the singleton ApplicationConfig with a small UI Settings panel."
    needs={<span>new <code>UIConfig</code> fields on <code>ApplicationConfig</code> + REST + UI.</span>}
  />
);

const SECTIONS: SectionDef[] = [
  { slug: "mediamanagement", label: "Media Management", panel: GeneralSettingsPanel },
  { slug: "profiles",        label: "Profiles",         panel: ProfilesPanel },
  { slug: "quality",         label: "Quality",          panel: QualityStub },
  { slug: "customformats",   label: "Custom Formats",   panel: CustomFormatsPanel },
  { slug: "indexers",        label: "Indexers",         panel: IndexersPanel },
  { slug: "downloadclients", label: "Download Clients", panel: DownloadClientsPanel },
  { slug: "importlists",     label: "Discovery Rules",  panel: DiscoveryRulesPanel },
  { slug: "connect",         label: "Connect",          panel: NotificationsPanel },
  { slug: "metadata",        label: "Metadata",         panel: MetadataStub },
  { slug: "metadatasource",  label: "Metadata Source",  panel: MetadataSourceStub },
  { slug: "tags",            label: "Tags",             panel: TagsPanel },
  { slug: "rootfolders",     label: "Root Folders",     panel: RootFoldersPanel },
  { slug: "blocklist",       label: "Blocklist",        panel: BlocklistPanel },
  { slug: "general",         label: "General",          panel: GeneralStub },
  { slug: "ui",              label: "UI",               panel: UiStub },
  { slug: "security",        label: "Security",         panel: SecurityPanel },
];

export function SettingsPage(): JSX.Element {
  const { section } = useParams<{ section: string }>();
  const active = SECTIONS.find((s) => s.slug === section);
  if (!active) return <Navigate to="/settings/mediamanagement" replace />;
  const Panel = active.panel;
  return (
    <PageContent title={active.label}>
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Save Changes"
            iconName={icons.SAVE}
            isDisabled
            onPress={() => { /* P-Settings will wire pending changes */ }}
          />
        </PageToolbarSection>
        <PageToolbarSection alignContent="right">
          <PageToolbarButton
            label="Advanced"
            iconName={icons.COGS}
            isDisabled
            onPress={() => { /* TODO: advanced settings */ }}
          />
        </PageToolbarSection>
      </PageToolbar>
      <PageContentBody>
        <Panel />
      </PageContentBody>
    </PageContent>
  );
}
