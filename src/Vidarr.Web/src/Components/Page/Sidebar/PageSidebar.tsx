/*
 * Adapted from Sonarr (GPL-3.0). Mirrors Sonarr's PageSidebar LINKS shape
 * one-for-one, adjusted to Vidarr's domain (artists / music videos) and the
 * pages we currently ship.
 */
import { useLocation } from "react-router-dom";
import type { IconDefinition } from "@fortawesome/fontawesome-svg-core";
import { icons } from "../../Icon/Icon";
import {
  PageSidebarItem,
  PageSidebarChildItem,
  type SidebarChild,
} from "./PageSidebarItem";
import styles from "./PageSidebar.module.css";

type SidebarLink = {
  icon: IconDefinition;
  label: string;
  to: string;
  alias?: string;
  children?: SidebarChild[];
};

const LINKS: SidebarLink[] = [
  {
    icon: icons.LIBRARY,
    label: "Library",
    to: "/library",
    alias: "/library",
    children: [
      { to: "/add", label: "Add Artist" },
    ],
  },
  { icon: icons.CALENDAR, label: "Calendar", to: "/calendar" },
  {
    icon: icons.ACTIVITY,
    label: "Activity",
    to: "/activity/queue",
    alias: "/activity",
    children: [
      { to: "/activity/queue",     label: "Queue" },
      { to: "/activity/history",   label: "History" },
      { to: "/activity/blocklist", label: "Blocklist" },
    ],
  },
  {
    icon: icons.WANTED,
    label: "Wanted",
    to: "/wanted/missing",
    alias: "/wanted",
    children: [
      { to: "/wanted/missing", label: "Missing" },
      { to: "/wanted/cutoff",  label: "Cutoff Unmet" },
    ],
  },
  {
    icon: icons.SETTINGS,
    label: "Settings",
    to: "/settings/mediamanagement",
    alias: "/settings",
    children: [
      { to: "/settings/mediamanagement", label: "Media Management" },
      { to: "/settings/profiles",        label: "Profiles" },
      { to: "/settings/quality",         label: "Custom Formats" },
      { to: "/settings/indexers",        label: "Indexers" },
      { to: "/settings/downloadclients", label: "Download Clients" },
      { to: "/settings/importlists",     label: "Discovery Rules" },
      { to: "/settings/connect",         label: "Connect" },
      { to: "/settings/tags",            label: "Tags" },
      { to: "/settings/rootfolders",     label: "Root Folders" },
      { to: "/settings/blocklist",       label: "Blocklist" },
      { to: "/settings/security",        label: "Security" },
    ],
  },
  {
    icon: icons.SYSTEM,
    label: "System",
    to: "/system/status",
    alias: "/system",
    children: [
      { to: "/system/status", label: "Status" },
      { to: "/system/tasks",  label: "Tasks" },
      { to: "/system/health", label: "Health" },
      { to: "/system/backup", label: "Backup" },
    ],
  },
];

function isParentActive(link: SidebarLink, pathname: string): boolean {
  const root = link.alias ?? link.to;
  return pathname.startsWith(root);
}

export function PageSidebar(): JSX.Element {
  const { pathname } = useLocation();
  return (
    <div className={styles.sidebarContainer}>
      <div className={styles.sidebar}>
        <ul className={styles.sidebarItems}>
          {LINKS.map((link) => {
            const isActiveParent = isParentActive(link, pathname);
            return (
              <li key={link.to} className={styles.linkGroup}>
                <ul>
                  <PageSidebarItem
                    to={link.to}
                    icon={link.icon}
                    label={link.label}
                    isActiveParent={isActiveParent}
                  />
                  {isActiveParent && link.children && (
                    <ul>
                      {link.children.map((c) => (
                        <PageSidebarChildItem key={c.to} to={c.to} label={c.label} />
                      ))}
                    </ul>
                  )}
                </ul>
              </li>
            );
          })}
        </ul>
      </div>
    </div>
  );
}
