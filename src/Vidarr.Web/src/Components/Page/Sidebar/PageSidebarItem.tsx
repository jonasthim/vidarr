/* Adapted from Sonarr (GPL-3.0). Single-level nav (no child items). */
import { NavLink } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import type { IconDefinition } from "@fortawesome/fontawesome-svg-core";
import classNames from "classnames";
import styles from "./PageSidebarItem.module.css";

type Props = {
  to: string;
  icon: IconDefinition;
  label: string;
};

export function PageSidebarItem({ to, icon, label }: Props): JSX.Element {
  return (
    <NavLink to={to}>
      {({ isActive }) => (
        <li className={classNames(styles.item, isActive && styles.isActiveItem)}>
          <div className={classNames(styles.link, isActive && styles.isActiveLink)}>
            <span className={styles.iconContainer}>
              <FontAwesomeIcon icon={icon} />
            </span>
            {label}
          </div>
        </li>
      )}
    </NavLink>
  );
}
