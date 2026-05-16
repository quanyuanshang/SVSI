import type { ReactNode } from "react";
import { StardewSpriteIcon } from "./StardewSpriteIcon";

interface StardewBadgeProps {
  children: ReactNode;
  iconKey?: string;
  fallbackIcon?: string;
  tone?: "ready" | "later" | "locked" | "neutral";
  className?: string;
}

export function StardewBadge({
  children,
  iconKey,
  fallbackIcon,
  tone = "neutral",
  className,
}: StardewBadgeProps) {
  return (
    <span className={["stardew-badge", `stardew-badge--${tone}`, className].filter(Boolean).join(" ")}>
      {iconKey ? (
        <StardewSpriteIcon
          className="stardew-badge__icon"
          fallback={fallbackIcon}
          size={14}
          spriteKey={iconKey}
        />
      ) : null}
      <span>{children}</span>
    </span>
  );
}
