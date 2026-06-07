/*
 * Shim: keeps the existing <PageHeader title=… actions=…/> API used across
 * pages but renders Sonarr's PageToolbar band + a PageContentBody-like inner
 * wrapper. This puts the slate toolbar OUTSIDE the padded body, matching
 * Sonarr's PageContent > PageToolbar > PageContentBody structure exactly.
 */
import { ReactNode } from "react";
import styles from "./PageHeader.module.css";

type Props = {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
};

export function PageHeader({ title, subtitle, actions }: Props): JSX.Element {
  return (
    <div className={styles.toolbar}>
      <div className={styles.left}>
        <div className={styles.title}>{title}</div>
        {subtitle && <div className={styles.subtitle}>{subtitle}</div>}
      </div>
      <div className={styles.right}>{actions}</div>
    </div>
  );
}
