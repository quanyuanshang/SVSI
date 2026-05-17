import type { ReactNode } from "react";
import { StardewSpriteIcon } from "./StardewSpriteIcon";

export type StorySectionTone =
  | "current"
  | "later"
  | "locked"
  | "unknown"
  | "triggered";

interface StorySectionPanelProps {
  title: string;
  count: number;
  tone: StorySectionTone;
  iconKey: string;
  fallbackIcon: string;
  children: ReactNode;
}

export function StorySectionPanel({
  title,
  count,
  tone,
  iconKey,
  fallbackIcon,
  children,
}: StorySectionPanelProps) {
  return (
    <section className={`story-section-panel story-section-panel--${tone}`}>
      <div className="story-section-panel__header">
        <h3>
          <StardewSpriteIcon fallback={fallbackIcon} size={22} spriteKey={iconKey} />
          {title}
        </h3>
        <span>{count}</span>
      </div>
      {children}
    </section>
  );
}
