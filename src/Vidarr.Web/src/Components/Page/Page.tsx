/* Adapted from Sonarr (GPL-3.0). Simplified for Vidarr's TanStack Query data layer. */
import { ReactNode } from "react";
import styles from "./Page.module.css";

type Props = { children: ReactNode };

export function Page({ children }: Props): JSX.Element {
  return <div className={styles.page}>{children}</div>;
}

export function PageMain({ children }: Props): JSX.Element {
  return <div className={styles.main}>{children}</div>;
}
