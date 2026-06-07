/* Adapted from Sonarr (GPL-3.0). Drops Scroller/scroll-position tracking. */
import { ReactNode } from "react";
import styles from "./PageContentBody.module.css";

type Props = { children: ReactNode };

export function PageContentBody({ children }: Props): JSX.Element {
  return (
    <div className={styles.contentBody}>
      <div className={styles.innerContentBody}>{children}</div>
    </div>
  );
}
