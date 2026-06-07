/* Adapted from Sonarr (GPL-3.0). Provides the standard 20px page gutter. */
import { ReactNode } from "react";
import styles from "./PageContentBody.module.css";

export function PageContentBody({ children }: { children: ReactNode }): JSX.Element {
  return (
    <div className={styles.contentBody}>
      <div className={styles.innerContentBody}>{children}</div>
    </div>
  );
}
