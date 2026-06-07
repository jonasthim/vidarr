/* Adapted from Sonarr (GPL-3.0). */
import { ReactNode } from "react";
import styles from "./FormGroup.module.css";

type Props = { label: ReactNode; children: ReactNode };

export function FormGroup({ label, children }: Props): JSX.Element {
  return (
    <div className={styles.group}>
      <div className={styles.label}>{label}</div>
      <div className={styles.input}>{children}</div>
    </div>
  );
}
