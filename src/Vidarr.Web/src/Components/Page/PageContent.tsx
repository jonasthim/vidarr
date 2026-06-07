/* Adapted from Sonarr (GPL-3.0). Drops the DocumentTitle/ErrorBoundary wrappers. */
import { ReactNode, useEffect } from "react";
import styles from "./PageContent.module.css";

type Props = { title?: string; children: ReactNode };

export function PageContent({ title, children }: Props): JSX.Element {
  useEffect(() => {
    if (title) {
      document.title = `${title} - Vidarr`;
    } else {
      document.title = "Vidarr";
    }
  }, [title]);
  return <div className={styles.content}>{children}</div>;
}
