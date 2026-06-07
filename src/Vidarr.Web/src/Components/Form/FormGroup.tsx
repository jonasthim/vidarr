/* Adapted from Sonarr Form/FormGroup (GPL-3.0). */
import { ReactNode } from "react";
import styles from "./FormGroup.module.css";

type Size = "extraSmall" | "small" | "medium" | "large";
type Props = { size?: Size; children: ReactNode };

export function FormGroup({ size = "small", children }: Props): JSX.Element {
  return <div className={`${styles.group} ${styles[size]}`}>{children}</div>;
}
