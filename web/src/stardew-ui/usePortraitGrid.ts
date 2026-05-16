import { useMemo } from "react";
import {
  derivePortraitGrid,
  type PortraitGrid,
  type PortraitGridOverride,
} from "./portraitFrame";
import { useAtlasNaturalSize, type AtlasSize } from "./stardewAssetResolver";

export function usePortraitGrid(
  hdUrl?: string | null,
  baseUrl?: string | null,
  override?: PortraitGridOverride | null,
): {
  grid: PortraitGrid | null;
  hdSize: AtlasSize | null;
  baseSize: AtlasSize | null;
} {
  const hdSize = useAtlasNaturalSize(hdUrl);
  const baseSize = useAtlasNaturalSize(baseUrl);
  const grid = useMemo(
    () => derivePortraitGrid(baseSize, hdSize, override),
    [baseSize, hdSize, override],
  );

  return { grid, hdSize, baseSize };
}
