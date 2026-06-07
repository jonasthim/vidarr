/* Adapted from Sonarr (GPL-3.0). */
import { ReactNode } from "react";
import classNames from "classnames";
import styles from "./PageToolbarSection.module.css";

type Align = "left" | "center" | "right";
type Props = { alignContent?: Align; children?: ReactNode };

export function PageToolbarSection({ alignContent = "left", children }: Props): JSX.Element {
  return (
    <div className={styles.sectionContainer}>
      <div className={classNames(styles.section, styles[alignContent])}>{children}</div>
    </div>
  );
}
