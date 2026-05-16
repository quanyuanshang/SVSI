import type { CSSProperties, ReactNode } from "react";
import { useStardewAssetResolver } from "../lib/stardewAssets";

type StardewPanelVariant = "default" | "darkWood" | "note" | "board" | "textbox";

interface StardewNineSlicePanelProps {
  as?: "div" | "section" | "aside" | "header";
  spriteKey?: string;
  variant?: StardewPanelVariant | string;
  className?: string;
  children: ReactNode;
  fallbackToCss?: boolean;
}

const VARIANT_SPRITES: Record<StardewPanelVariant, string[]> = {
  default: ["ui.panel.default"],
  darkWood: ["ui.panel.darkWood", "ui.panel.default"],
  note: ["ui.panel.note", "ui.panel.textbox", "ui.panel.default"],
  board: ["ui.panel.board", "ui.panel.default"],
  textbox: ["ui.panel.textbox", "ui.panel.default"],
};

export function StardewNineSlicePanel({
  as = "div",
  spriteKey,
  variant = "default",
  className,
  children,
  fallbackToCss = true,
}: StardewNineSlicePanelProps) {
  const resolver = useStardewAssetResolver();
  const candidateSpriteKeys = spriteKey
    ? [spriteKey]
    : VARIANT_SPRITES[variant as StardewPanelVariant] ?? [`ui.panel.${variant}`, "ui.windowBorder.default"];
  const sprite = candidateSpriteKeys
    .map((spriteKey) => resolver.getSprite(spriteKey))
    .find(Boolean);
  const Component = as;
  const canUseBorderImage = sprite?.nineSlice && sprite.assetType === "image";

  const style = canUseBorderImage
    ? ({
        "--stardew-border-source": `url("${sprite.atlasUrl}")`,
        "--stardew-border-slice": `${sprite.nineSlice.top} ${sprite.nineSlice.right} ${sprite.nineSlice.bottom} ${sprite.nineSlice.left}`,
        "--stardew-border-width": `${sprite.nineSlice.top}px ${sprite.nineSlice.right}px ${sprite.nineSlice.bottom}px ${sprite.nineSlice.left}px`,
      } as CSSProperties)
    : undefined;

  return (
    <Component
      className={[
        "stardew-nine-slice",
        `stardew-nine-slice--${variant}`,
        canUseBorderImage || !fallbackToCss
          ? "stardew-nine-slice--asset"
          : "stardew-nine-slice--fallback",
        className,
      ]
        .filter(Boolean)
        .join(" ")}
      style={style}
    >
      {children}
    </Component>
  );
}
