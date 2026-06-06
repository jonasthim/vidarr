import { useState } from "react";
import { AddArtistPage } from "./pages/AddArtist";
import { LibraryPage } from "./pages/Library";
import { QueuePage } from "./pages/Queue";
import { SettingsPage } from "./pages/Settings";
import { CommandsPage } from "./pages/Commands";
import { HistoryPage } from "./pages/History";

type Tab = "library" | "add" | "queue" | "history" | "commands" | "settings";

export function App(): JSX.Element {
  const [tab, setTab] = useState<Tab>("library");

  return (
    <div className="app">
      <header className="topbar">
        <h1>Vidarr</h1>
        <nav>
          <button
            type="button"
            className={tab === "library" ? "active" : ""}
            onClick={() => setTab("library")}
          >
            Library
          </button>
          <button
            type="button"
            className={tab === "add" ? "active" : ""}
            onClick={() => setTab("add")}
          >
            Add Artist
          </button>
          <button
            type="button"
            className={tab === "queue" ? "active" : ""}
            onClick={() => setTab("queue")}
          >
            Queue
          </button>
          <button
            type="button"
            className={tab === "history" ? "active" : ""}
            onClick={() => setTab("history")}
          >
            History
          </button>
          <button
            type="button"
            className={tab === "commands" ? "active" : ""}
            onClick={() => setTab("commands")}
          >
            Commands
          </button>
          <button
            type="button"
            className={tab === "settings" ? "active" : ""}
            onClick={() => setTab("settings")}
          >
            Settings
          </button>
        </nav>
      </header>
      <main>
        {tab === "library" && <LibraryPage />}
        {tab === "add" && <AddArtistPage />}
        {tab === "queue" && <QueuePage />}
        {tab === "history" && <HistoryPage />}
        {tab === "commands" && <CommandsPage />}
        {tab === "settings" && <SettingsPage />}
      </main>
    </div>
  );
}
