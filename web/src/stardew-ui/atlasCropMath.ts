import type { StardewRect } from "./stardewAssetResolver";

export function getAtlasPoint(image: HTMLImageElement, clientX: number, clientY: number) {
  const bounds = image.getBoundingClientRect();
  const scaleX = image.naturalWidth / bounds.width;
  const scaleY = image.naturalHeight / bounds.height;

  return {
    x: clamp(Math.floor((clientX - bounds.left) * scaleX), 0, image.naturalWidth),
    y: clamp(Math.floor((clientY - bounds.top) * scaleY), 0, image.naturalHeight),
  };
}

export function normalizeRect(startX: number, startY: number, endX: number, endY: number): StardewRect {
  const x = Math.min(startX, endX);
  const y = Math.min(startY, endY);

  return {
    x,
    y,
    w: Math.max(1, Math.abs(endX - startX)),
    h: Math.max(1, Math.abs(endY - startY)),
  };
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value));
}
