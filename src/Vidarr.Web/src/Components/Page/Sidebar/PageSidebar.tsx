/* Adapted from Sonarr (GPL-3.0). Replaces our hand-rolled Sidebar.tsx. */
import { icons } from "../../Icon/Icon";
import { PageSidebarItem } from "./PageSidebarItem";
import styles from "./PageSidebar.module.css";

const NAV = [
  { to: "/library",  icon: icons.LIBRARY,    label: "Library" },
  { to: "/add",      icon: icons.ADD,        label: "Add Artist" },
  { to: "/calendar", icon: icons.CALENDAR,   label: "Calendar" },
  { to: "/activity", icon: icons.ACTIVITY,   label: "Activity" },
  { to: "/wanted",   icon: icons.WANTED,     label: "Wanted" },
  { to: "/settings", icon: icons.SETTINGS,   label: "Settings" },
  { to: "/system",   icon: icons.SYSTEM,     label: "System" },
];

export function PageSidebar(): JSX.Element {
  return (
    <div className={styles.sidebarContainer}>
      <div className={styles.sidebar}>
        <ul className={styles.sidebarItems}>
          {NAV.map((n) => (
            <PageSidebarItem key={n.to} {...n} />
          ))}
        </ul>
      </div>
    </div>
  );
}
