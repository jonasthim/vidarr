/*
 * Shim: keeps the existing <PageHeader title=… actions=…/> API used across
 * pages but renders in Sonarr's PageToolbar style (slate band, 60px tall, with
 * a left section for the title and a right section for action buttons).
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
