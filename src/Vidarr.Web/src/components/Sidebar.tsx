import { ReactNode } from "react";
import { NavLink } from "react-router-dom";
import {
  Music,
  Plus,
  Calendar as CalendarIcon,
  Activity,
  AlertCircle,
  Settings as SettingsIcon,
  Server,
  LogOut,
} from "lucide-react";

type NavEntry = {
  to: string;
  label: string;
  icon: ReactNode;
};

const NAV: NavEntry[] = [
  { to: "/library",  label: "Library",     icon: <Music /> },
  { to: "/add",      label: "Add Artist",  icon: <Plus /> },
  { to: "/calendar", label: "Calendar",    icon: <CalendarIcon /> },
  { to: "/activity", label: "Activity",    icon: <Activity /> },
  { to: "/wanted",   label: "Wanted",      icon: <AlertCircle /> },
  { to: "/settings", label: "Settings",    icon: <SettingsIcon /> },
  { to: "/system",   label: "System",      icon: <Server /> },
];

type Props = {
  showLogout: boolean;
  onLogout: () => void;
  username?: string | null;
};

export function Sidebar({ showLogout, onLogout, username }: Props): JSX.Element {
  return (
    <aside className="sidebar">
      <ul className="sidebar-nav">
        {NAV.map((n) => (
          <li key={n.to}>
            <NavLink
              to={n.to}
              className={({ isActive }) => (isActive ? "active" : "")}
            >
              {n.icon}
              <span>{n.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
      {showLogout && (
        <div className="sidebar-bottom">
          <button type="button" className="ghost" onClick={onLogout} title={username ?? undefined}>
            <LogOut />
            <span className="label">Sign out</span>
          </button>
        </div>
      )}
    </aside>
  );
}
