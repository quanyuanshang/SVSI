import type { ButtonHTMLAttributes, CSSProperties, ReactNode } from "react";
import { useStardewAssetResolver } from "../lib/stardewAssets";

interface StardewButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode;
  tone?: "primary" | "quiet";
}

export function StardewButton({
  children,
  className,
  tone = "primary",
  type = "button",
  ...props
}: StardewButtonProps) {
  const resolver = useStardewAssetResolver();
  const sprite = resolver.getSprite("ui.button.default");
  const style = sprite?.nineSlice
    ? ({
        "--stardew-button-source": `url("${sprite.atlasUrl}")`,
        "--stardew-button-slice": `${sprite.nineSlice.top} ${sprite.nineSlice.right} ${sprite.nineSlice.bottom} ${sprite.nineSlice.left}`,
        "--stardew-button-width": `${sprite.nineSlice.top}px ${sprite.nineSlice.right}px ${sprite.nineSlice.bottom}px ${sprite.nineSlice.left}px`,
      } as CSSProperties)
    : undefined;

  return (
    <button
      {...props}
      className={[
        "stardew-button",
        `stardew-button--${tone}`,
        sprite?.nineSlice ? "stardew-button--asset" : "stardew-button--fallback",
        className,
      ]
        .filter(Boolean)
        .join(" ")}
      style={style}
      type={type}
    >
      {children}
    </button>
  );
}
