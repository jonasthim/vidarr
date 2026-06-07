/* Adapted from Sonarr (GPL-3.0). */
import { ReactNode } from "react";
import styles from "./Alert.module.css";

type Kind = "info" | "success" | "warning" | "danger";

export function Alert({ kind = "info", children }: { kind?: Kind; children: ReactNode }): JSX.Element {
  return <div className={`${styles.alert} ${styles[kind]}`}>{children}</div>;
}
