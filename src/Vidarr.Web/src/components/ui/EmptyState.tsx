import { ReactNode } from "react";
import styles from "./EmptyState.module.css";

type Props = {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
};

export function EmptyState({ icon, title, description, action }: Props): JSX.Element {
  return (
    <div className={styles.empty}>
      {icon && <div className={styles.icon}>{icon}</div>}
      <h2 className={styles.title}>{title}</h2>
      {description && <p className={styles.description}>{description}</p>}
      {action && <div className={styles.action}>{action}</div>}
    </div>
  );
}
