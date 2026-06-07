/*
 * Adapted from Sonarr (GPL-3.0). Drops the DocumentTitle/ErrorBoundary
 * wrappers. Pages render `<PageHeader title actions/>` (the legacy shim,
 * which is now a Sonarr-style toolbar) followed by their content; the
 * scaffolded padding lives in the toolbar-aware body wrapper below so the
 * toolbar bleeds to the edges and the content gets the standard 20px gutters.
 */
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
