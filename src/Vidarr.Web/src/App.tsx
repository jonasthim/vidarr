import { useState } from "react";
import { AddArtistPage } from "./pages/AddArtist";
import { LibraryPage } from "./pages/Library";
import { QueuePage } from "./pages/Queue";

type Tab = "library" | "add" | "queue";

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
        </nav>
      </header>
      <main>
        {tab === "library" && <LibraryPage />}
        {tab === "add" && <AddArtistPage />}
        {tab === "queue" && <QueuePage />}
      </main>
    </div>
  );
}
