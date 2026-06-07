/* Adapted from Sonarr (GPL-3.0). */
import { ReactNode } from "react";
import classNames from "classnames";
import styles from "./Label.module.css";

export type LabelKind =
  | "default"
  | "primary"
  | "info"
  | "success"
  | "warning"
  | "danger"
  | "disabled"
  | "inverse"
  | "purple"
  | "pink";

type Props = { kind?: LabelKind; outline?: boolean; children: ReactNode };

export function Label({ kind = "default", outline, children }: Props): JSX.Element {
  return (
    <span className={classNames(styles.label, styles[kind], outline && styles.outline)}>
      {children}
    </span>
  );
}
