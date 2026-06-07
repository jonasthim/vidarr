/* Shim: forwards to the new Sonarr-derived Label component. */
import { ReactNode } from "react";
import { Label, type LabelKind } from "../../Components/Label/Label";

type Variant =
  | "monitored"
  | "unmonitored"
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "muted";

const VARIANT_TO_KIND: Record<Variant, LabelKind> = {
  monitored: "success",
  unmonitored: "default",
  success: "success",
  warning: "warning",
  danger: "danger",
  info: "info",
  muted: "disabled",
};

type Props = { variant?: Variant; children: ReactNode };

export function StatusPill({ variant = "muted", children }: Props): JSX.Element {
  return <Label kind={VARIANT_TO_KIND[variant]}>{children}</Label>;
}
