/* Adapted from Sonarr (GPL-3.0). Supports parent + child rendering. */
import { NavLink, useLocation } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import type { IconDefinition } from "@fortawesome/fontawesome-svg-core";
import classNames from "classnames";
import styles from "./PageSidebarItem.module.css";

export type SidebarChild = { to: string; label: string };

type ParentProps = {
  to: string;
  icon: IconDefinition;
  label: string;
  isActiveParent: boolean;
};

export function PageSidebarItem({ to, icon, label, isActiveParent }: ParentProps): JSX.Element {
  const location = useLocation();
  const isActiveSelf = location.pathname === to;
  return (
    <li className={classNames(styles.item, isActiveSelf && styles.isActiveItem, isActiveParent && styles.isActiveParentItem)}>
      <NavLink to={to} className={({ isActive }) => classNames(styles.link, isActive && styles.isActiveLink)}>
        <span className={styles.iconContainer}>
          <FontAwesomeIcon icon={icon} />
        </span>
        {label}
      </NavLink>
    </li>
  );
}

type ChildProps = { to: string; label: string };

export function PageSidebarChildItem({ to, label }: ChildProps): JSX.Element {
  return (
    <li className={styles.item}>
      <NavLink
        to={to}
        className={({ isActive }) =>
          classNames(styles.link, styles.childLink, isActive && styles.isActiveLink)
        }
      >
        {label}
      </NavLink>
    </li>
  );
}
