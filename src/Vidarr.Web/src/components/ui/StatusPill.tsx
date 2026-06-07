import { ReactNode } from "react";

type Variant =
  | "monitored"
  | "unmonitored"
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "muted";

type Props = {
  variant?: Variant;
  children: ReactNode;
};

export function StatusPill({ variant = "muted", children }: Props): JSX.Element {
  return <span className={`pill ${variant}`}>{children}</span>;
}
