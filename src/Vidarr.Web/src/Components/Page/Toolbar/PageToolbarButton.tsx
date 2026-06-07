/* Adapted from Sonarr (GPL-3.0). */
import { ReactNode } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import type { IconDefinition } from "@fortawesome/fontawesome-svg-core";
import classNames from "classnames";
import styles from "./PageToolbarButton.module.css";

type CommonProps = {
  label: string;
  iconName?: IconDefinition;
  isDisabled?: boolean;
  isActive?: boolean;
  children?: ReactNode;
};

type Props =
  | (CommonProps & { onPress: () => void; to?: never })
  | (CommonProps & { to: string; onPress?: never });

export function PageToolbarButton(props: Props): JSX.Element {
  const { label, iconName, isDisabled, isActive } = props;
  const className = classNames(
    styles.toolbarButton,
    isDisabled && styles.isDisabled,
    isActive && styles.isActive,
  );
  const inner = (
    <>
      <span className={styles.iconContainer}>
        {iconName && <FontAwesomeIcon icon={iconName} />}
      </span>
      <span className={styles.label}>{label}</span>
    </>
  );
  if ("to" in props && props.to) {
    return <Link to={props.to} className={className}>{inner}</Link>;
  }
  return (
    <button type="button" disabled={isDisabled} onClick={props.onPress} className={className}>
      {inner}
    </button>
  );
}
