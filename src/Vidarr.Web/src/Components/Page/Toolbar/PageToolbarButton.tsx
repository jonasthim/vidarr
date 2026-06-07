/* Adapted from Sonarr (GPL-3.0). Vertical icon-stacked toolbar button. */
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import type { IconDefinition } from "@fortawesome/fontawesome-svg-core";
import classNames from "classnames";
import styles from "./PageToolbarButton.module.css";

type CommonProps = {
  label: string;
  iconName: IconDefinition;
  isDisabled?: boolean;
  isActive?: boolean;
  isSpinning?: boolean;
};

type Props =
  | (CommonProps & { onPress: () => void; to?: never })
  | (CommonProps & { to: string; onPress?: never });

export function PageToolbarButton(props: Props): JSX.Element {
  const { label, iconName, isDisabled, isActive, isSpinning } = props;
  const className = classNames(
    styles.toolbarButton,
    isDisabled && styles.isDisabled,
    isActive && styles.isActive,
  );
  const inner = (
    <>
      <span className={styles.iconContainer}>
        <FontAwesomeIcon icon={iconName} className={isSpinning ? styles.spinning : undefined} />
      </span>
      <span className={styles.label}>{label}</span>
    </>
  );
  if ("to" in props && props.to) {
    return (
      <Link to={props.to} className={className} title={label}>
        {inner}
      </Link>
    );
  }
  return (
    <button
      type="button"
      disabled={isDisabled}
      onClick={props.onPress}
      className={className}
      title={label}
    >
      {inner}
    </button>
  );
}
