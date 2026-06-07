import { ReactNode } from "react";
import { NavLink } from "react-router-dom";

export type TabEntry = {
  to: string;
  label: ReactNode;
  end?: boolean;
};

type Props = {
  tabs: TabEntry[];
};

export function Tabs({ tabs }: Props): JSX.Element {
  return (
    <nav className="tabs">
      {tabs.map((t) => (
        <NavLink
          key={t.to}
          to={t.to}
          end={t.end}
          className={({ isActive }) => (isActive ? "active" : "")}
        >
          {t.label}
        </NavLink>
      ))}
    </nav>
  );
}
