import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./api";
import { AddArtistPage } from "./pages/AddArtist";
import { LibraryPage } from "./pages/Library";
import { QueuePage } from "./pages/Queue";
import { SettingsPage } from "./pages/Settings";
import { CommandsPage } from "./pages/Commands";
import { HistoryPage } from "./pages/History";
import { HealthPage } from "./pages/Health";
import { LoginPage } from "./pages/Login";

type Tab = "library" | "add" | "queue" | "history" | "commands" | "health" | "settings";

export function App(): JSX.Element {
  const queryClient = useQueryClient();
  const authStatusQuery = useQuery({
    queryKey: ["auth-status"],
    queryFn: api.getAuthStatus,
    refetchInterval: false,
    retry: false,
  });
  const logout = useMutation({
    mutationFn: api.logout,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["auth-status"] }),
  });
  const [tab, setTab] = useState<Tab>("library");

  if (authStatusQuery.isLoading) return <p>Loading…</p>;

  const status = authStatusQuery.data;
  const requiresLogin = status?.enabled === true && status?.authenticated === false;
  if (requiresLogin) {
    return <LoginPage onSuccess={() => authStatusQuery.refetch()} />;
  }

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
            className={tab === "health" ? "active" : ""}
            onClick={() => setTab("health")}
          >
            Health
          </button>
          <button
            type="button"
            className={tab === "settings" ? "active" : ""}
            onClick={() => setTab("settings")}
          >
            Settings
          </button>
          {status?.enabled && (
            <button
              type="button"
              className="logout"
              onClick={() => logout.mutate()}
              disabled={logout.isPending}
              title={status.username ? `Signed in as ${status.username}` : undefined}
            >
              Sign out
            </button>
          )}
        </nav>
      </header>
      <main>
        {tab === "library" && <LibraryPage />}
        {tab === "add" && <AddArtistPage />}
        {tab === "queue" && <QueuePage />}
        {tab === "history" && <HistoryPage />}
        {tab === "commands" && <CommandsPage />}
        {tab === "health" && <HealthPage />}
        {tab === "settings" && <SettingsPage />}
      </main>
    </div>
  );
}
