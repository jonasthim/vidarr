/* Adapted from Sonarr Form/FormInputGroup (GPL-3.0). */
import { ReactNode } from "react";
import styles from "./FormInputGroup.module.css";

type Props = {
  children: ReactNode;
  helpText?: ReactNode;
};

export function FormInputGroup({ children, helpText }: Props): JSX.Element {
  return (
    <div className={styles.inputGroupContainer}>
      <div className={styles.inputGroup}>
        <div className={styles.inputContainer}>{children}</div>
      </div>
      {helpText && <div className={styles.helpText}>{helpText}</div>}
    </div>
  );
}
