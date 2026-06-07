/* Adapted from Sonarr Components/FieldSet (GPL-3.0). */
import { ReactNode } from "react";
import styles from "./FieldSet.module.css";

type Props = { legend: ReactNode; children: ReactNode };

export function FieldSet({ legend, children }: Props): JSX.Element {
  return (
    <fieldset className={styles.fieldSet}>
      <legend className={styles.legend}>{legend}</legend>
      <div className={styles.body}>{children}</div>
    </fieldset>
  );
}
