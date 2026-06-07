/* Adapted from Sonarr (GPL-3.0). Simpler spinner; no ripple animation. */
import styles from "./LoadingIndicator.module.css";

export function LoadingIndicator(): JSX.Element {
  return (
    <div className={styles.loading}>
      <div className={styles.spinner} />
    </div>
  );
}
