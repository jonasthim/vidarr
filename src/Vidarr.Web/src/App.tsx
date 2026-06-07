import { useEffect, useState } from "react";
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

  // After a successful forms-auth login the SPA may still be running with an
  // empty window.VIDARR_API_KEY (production HTML deliberately omits the key
  // until the user is authenticated). Pull it from the API once we know the
  // session is live, so panels like Settings → Security can render it and so
  // any /api calls made without the session cookie still work.
  useEffect(() => {
    const status = authStatusQuery.data;
    const w = window as { VIDARR_API_KEY?: string };
    if (status?.authenticated && !w.VIDARR_API_KEY) {
      api
        .getApiKey()
        .then((r) => {
          w.VIDARR_API_KEY = r.apiKey;
        })
        .catch(() => {
          /* leave VIDARR_API_KEY empty; cookie auth still covers /api/v1/* */
        });
    }
  }, [authStatusQuery.data]);

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
