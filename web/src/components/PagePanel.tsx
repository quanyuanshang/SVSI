import type { ReactNode } from "react";

type PagePanelProps = {
  children: ReactNode;
  className?: string;
  variant?: "default" | "sidebar" | "header" | "main";
};

export function PagePanel({
  children,
  className,
  variant = "default",
}: PagePanelProps) {
  return (
    <div
      className={[
        "sd-page-panel",
        `sd-page-panel--${variant}`,
        className,
      ]
        .filter(Boolean)
        .join(" ")}
    >
      <div className="sd-page-panel__edge sd-page-panel__edge--top" />
      <div className="sd-page-panel__edge sd-page-panel__edge--right" />
      <div className="sd-page-panel__edge sd-page-panel__edge--bottom" />
      <div className="sd-page-panel__edge sd-page-panel__edge--left" />
      <div className="sd-page-panel__content">{children}</div>
    </div>
  );
}
