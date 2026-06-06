import { useState } from "react";
import { ProfilesPanel } from "../components/ProfilesPanel";
import { RootFoldersPanel } from "../components/RootFoldersPanel";
import { TagsPanel } from "../components/TagsPanel";
import { GeneralSettingsPanel } from "../components/GeneralSettingsPanel";
import { IndexersPanel } from "../components/IndexersPanel";
import { DownloadClientsPanel } from "../components/DownloadClientsPanel";
import { CustomFormatsPanel } from "../components/CustomFormatsPanel";
import { BlocklistPanel } from "../components/BlocklistPanel";
import { DiscoveryRulesPanel } from "../components/DiscoveryRulesPanel";

type Pane =
  | "general"
  | "profiles"
  | "customformats"
  | "rootfolders"
  | "tags"
  | "indexers"
  | "downloadclients"
  | "blocklist"
  | "discoveryrules";

export function SettingsPage(): JSX.Element {
  const [pane, setPane] = useState<Pane>("general");
  return (
    <section className="settings">
      <aside className="settings-nav">
        <h2>Settings</h2>
        <ul>
          {(
            [
              ["general", "Media Management"],
              ["profiles", "Quality Profiles"],
              ["customformats", "Custom Formats"],
              ["indexers", "Indexers"],
              ["downloadclients", "Download Clients"],
              ["discoveryrules", "Discovery Rules"],
              ["rootfolders", "Root Folders"],
              ["tags", "Tags"],
              ["blocklist", "Blocklist"],
            ] as const
          ).map(([key, label]) => (
            <li
              key={key}
              className={pane === key ? "active" : ""}
              onClick={() => setPane(key)}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => {
                if (e.key === "Enter") setPane(key);
              }}
            >
              {label}
            </li>
          ))}
        </ul>
      </aside>
      <div className="settings-pane">
        {pane === "general" && <GeneralSettingsPanel />}
        {pane === "profiles" && <ProfilesPanel />}
        {pane === "customformats" && <CustomFormatsPanel />}
        {pane === "indexers" && <IndexersPanel />}
        {pane === "downloadclients" && <DownloadClientsPanel />}
        {pane === "discoveryrules" && <DiscoveryRulesPanel />}
        {pane === "rootfolders" && <RootFoldersPanel />}
        {pane === "tags" && <TagsPanel />}
        {pane === "blocklist" && <BlocklistPanel />}
      </div>
    </section>
  );
}
