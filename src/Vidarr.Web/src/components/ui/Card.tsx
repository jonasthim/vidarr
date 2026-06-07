/*
 * Shim: keeps the existing <Card title=… actions=…> API but renders in
 * Sonarr's content-block style (no card chrome — just a header band over a
 * plain content area, matching how Sonarr structures Settings panels).
 */
import { ReactNode } from "react";
import styles from "./Card.module.css";

type Props = {
  title?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
};

export function Card({ title, actions, children, className }: Props): JSX.Element {
  return (
    <section className={`${styles.card}${className ? ` ${className}` : ""}`}>
      {(title || actions) && (
        <header className={styles.header}>
          {title && <h2 className={styles.title}>{title}</h2>}
          {actions && <div className={styles.actions}>{actions}</div>}
        </header>
      )}
      <div className={styles.body}>{children}</div>
    </section>
  );
}
