/* Adapted from Sonarr (GPL-3.0). */
import { ReactNode } from "react";
import styles from "./PageToolbar.module.css";

type Props = { children?: ReactNode };

export function PageToolbar({ children }: Props): JSX.Element {
  return <div className={styles.toolbar}>{children}</div>;
}
