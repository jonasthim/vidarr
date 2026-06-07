/* Adapted from Sonarr Form/FormLabel (GPL-3.0). */
import { ReactNode } from "react";
import styles from "./FormLabel.module.css";

type Size = "small" | "large";
type Props = { size?: Size; htmlFor?: string; children: ReactNode };

export function FormLabel({ size = "small", htmlFor, children }: Props): JSX.Element {
  return (
    <label htmlFor={htmlFor} className={`${styles.label} ${styles[size]}`}>
      {children}
    </label>
  );
}
