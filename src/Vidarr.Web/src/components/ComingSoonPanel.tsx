import { ReactNode } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { icons } from "../Components/Icon/Icon";
import { Card } from "./ui";

type Props = {
  title: string;
  description: ReactNode;
  needs?: ReactNode;
};

/**
 * Generic placeholder used for Sonarr menu items that exist in our nav for
 * parity but don't have backend support yet. Renders a Card with what the
 * page will do and what's needed before it can land.
 */
export function ComingSoonPanel({ title, description, needs }: Props): JSX.Element {
  return (
    <Card title={title}>
      <div style={{ textAlign: "center", padding: "40px 20px", color: "var(--helpTextColor)" }}>
        <div style={{ fontSize: 40, marginBottom: 16, color: "var(--themeLightColor)" }}>
          <FontAwesomeIcon icon={icons.COG} />
        </div>
        <h3 style={{ margin: "0 0 8px", color: "var(--textColor)" }}>Coming soon</h3>
        <p style={{ margin: "0 0 16px" }}>{description}</p>
        {needs && (
          <div
            style={{
              maxWidth: 600,
              margin: "0 auto",
              padding: 16,
              background: "var(--themeDarkColor)",
              borderRadius: 4,
              textAlign: "left",
              fontSize: "var(--smallFontSize)",
            }}
          >
            <strong style={{ color: "var(--white)" }}>Needs:</strong> {needs}
          </div>
        )}
      </div>
    </Card>
  );
}
