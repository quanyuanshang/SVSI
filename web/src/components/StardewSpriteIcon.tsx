import type { CSSProperties, ReactNode } from "react";
import {
  buildSpriteBackgroundStyle,
  computeSpriteRenderScale,
  useAtlasNaturalSize,
  useStardewAssetResolver,
} from "../lib/stardewAssets";

interface StardewSpriteIconProps {
  spriteKey: string;
  size?: number;
  scale?: number;
  title?: string;
  fallback?: ReactNode;
  className?: string;
}

export function StardewSpriteIcon({
  spriteKey,
  size = 18,
  scale = 2,
  title,
  fallback,
  className,
}: StardewSpriteIconProps) {
  const resolver = useStardewAssetResolver();
  const sprite = resolver.getSprite(spriteKey);
  const atlasSize = useAtlasNaturalSize(sprite?.atlasUrl);

  if (!sprite) {
    return (
      <span
        aria-hidden={title ? undefined : true}
        aria-label={title}
        className={["stardew-sprite-icon", "stardew-sprite-icon--fallback", "checkerboard", className]
          .filter(Boolean)
          .join(" ")}
        role={title ? "img" : undefined}
        style={{ width: size, height: size }}
        title={title}
      >
        {fallback}
      </span>
    );
  }

  const hasAtlasDimensions =
    atlasSize != null && atlasSize.width > 0 && atlasSize.height > 0;
  const targetScale = computeSpriteRenderScale(sprite, { scale, size });
  const cssScale = hasAtlasDimensions ? targetScale : 1;
  const needsTransformScale = !hasAtlasDimensions && targetScale !== 1;

  const innerStyle = buildSpriteBackgroundStyle(sprite, {
    scale: cssScale,
    atlasSize: hasAtlasDimensions ? atlasSize : null,
  });

  const outerStyle: CSSProperties | undefined = needsTransformScale
    ? {
        width: size,
        height: size,
        display: "inline-grid",
        placeItems: "center",
      }
    : undefined;

  const transformWrapStyle: CSSProperties | undefined = needsTransformScale
    ? {
        transform: `scale(${targetScale})`,
        transformOrigin: "center center",
      }
    : undefined;

  const icon = (
    <span
      aria-hidden={title ? undefined : true}
      aria-label={title}
      className={["stardew-sprite-icon", "stardew-sprite-icon--asset", "checkerboard", className]
        .filter(Boolean)
        .join(" ")}
      role={title ? "img" : undefined}
      style={transformWrapStyle ? { ...innerStyle, ...transformWrapStyle } : innerStyle}
      title={title}
    />
  );

  if (!outerStyle) {
    return icon;
  }

  return (
    <span className="stardew-sprite-icon-host" style={outerStyle}>
      {icon}
    </span>
  );
}
