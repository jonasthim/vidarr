import { useState } from "react";
import { ProfilesPanel } from "../components/ProfilesPanel";
import { RootFoldersPanel } from "../components/RootFoldersPanel";
import { TagsPanel } from "../components/TagsPanel";
import { GeneralSettingsPanel } from "../components/GeneralSettingsPanel";
import { IndexersPanel } from "../components/IndexersPanel";

type Pane = "general" | "profiles" | "rootfolders" | "tags" | "indexers";

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
              ["indexers", "Indexers"],
              ["rootfolders", "Root Folders"],
              ["tags", "Tags"],
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
        {pane === "indexers" && <IndexersPanel />}
        {pane === "rootfolders" && <RootFoldersPanel />}
        {pane === "tags" && <TagsPanel />}
      </div>
    </section>
  );
}
