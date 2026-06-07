/* Adapted from Sonarr (GPL-3.0). Simplified — no virtualization, no sort UI. */
import { ReactNode } from "react";
import styles from "./Table.module.css";

export function Table({ children }: { children: ReactNode }): JSX.Element {
  return (
    <div className={styles.tableContainer}>
      <table className={styles.table}>{children}</table>
    </div>
  );
}

export function TableHeader({ children }: { children: ReactNode }): JSX.Element {
  return <thead><tr>{children}</tr></thead>;
}

export function TableHeaderCell({ children }: { children?: ReactNode }): JSX.Element {
  return <th className={styles.headerCell}>{children}</th>;
}

export function TableBody({ children }: { children: ReactNode }): JSX.Element {
  return <tbody>{children}</tbody>;
}

export function TableRow({ children }: { children: ReactNode }): JSX.Element {
  return <tr className={styles.row}>{children}</tr>;
}

export function TableRowCell({
  children,
  actions,
}: {
  children?: ReactNode;
  actions?: boolean;
}): JSX.Element {
  return (
    <td className={`${styles.cell}${actions ? " " + styles.actions : ""}`}>{children}</td>
  );
}
