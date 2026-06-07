import { ReactNode } from "react";

type Props = {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
};

export function EmptyState({ icon, title, description, action }: Props): JSX.Element {
  return (
    <div className="empty-state">
      {icon}
      <h2>{title}</h2>
      {description && <p>{description}</p>}
      {action && <div style={{ marginTop: "var(--space-4)" }}>{action}</div>}
    </div>
  );
}
