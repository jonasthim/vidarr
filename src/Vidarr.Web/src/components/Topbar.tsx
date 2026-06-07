import { Search } from "lucide-react";

export function Topbar(): JSX.Element {
  return (
    <header className="topbar">
      <h1>Vidarr</h1>
      <div className="topbar-spacer" />
      <div className="topbar-search" title="Library search — coming soon">
        <Search size={14} />
        <input
          type="search"
          placeholder="Search…"
          disabled
          aria-label="Library search (coming soon)"
        />
      </div>
    </header>
  );
}
