import type { CSSProperties } from "react";
import type { AtlasSize } from "./stardewAssetResolver";

export const VANILLA_PORTRAIT_FRAME = 64;
export const PORTRAIT_FRAME_SIZE = VANILLA_PORTRAIT_FRAME;

export const PORTRAIT_DISPLAY_SIZES = {
  sm: 34,
  md: 46,
  lg: 72,
} as const;

export type PortraitDisplaySize = keyof typeof PORTRAIT_DISPLAY_SIZES;

export type PortraitFrameRect = {
  x: number;
  y: number;
  w: number;
  h: number;
  expressionIndex: number;
};

export type PortraitGridSource = "derived" | "manual" | "fallback";

export type PortraitGrid = {
  baseColumns: number;
  baseRows: number;
  frameWidth: number;
  frameHeight: number;
  baseSheetWidth: number;
  baseSheetHeight: number;
  hdSheetWidth: number;
  hdSheetHeight: number;
  source: PortraitGridSource;
  warning?: string;
};

export type PortraitGridOverride = {
  baseColumns?: number;
  baseRows?: number;
  frameWidth?: number;
  frameHeight?: number;
};

const COMMON_FALLBACK_COLUMNS = [2, 1, 4, 8];

export function deriveBaseGrid(baseSize: AtlasSize): Pick<PortraitGrid, "baseColumns" | "baseRows"> {
  return {
    baseColumns: Math.max(1, Math.floor(baseSize.width / VANILLA_PORTRAIT_FRAME)),
    baseRows: Math.max(1, Math.floor(baseSize.height / VANILLA_PORTRAIT_FRAME)),
  };
}

export function derivePortraitGrid(
  baseSize: AtlasSize | null,
  hdSize: AtlasSize | null,
  override?: PortraitGridOverride | null,
): PortraitGrid | null {
  if (!hdSize?.width || !hdSize.height) {
    return null;
  }

  if (override?.baseColumns && override.baseRows) {
    return finalizeGrid({
      baseColumns: override.baseColumns,
      baseRows: override.baseRows,
      frameWidth: override.frameWidth ?? hdSize.width / override.baseColumns,
      frameHeight: override.frameHeight ?? hdSize.height / override.baseRows,
      baseSheetWidth: baseSize?.width ?? override.baseColumns * VANILLA_PORTRAIT_FRAME,
      baseSheetHeight: baseSize?.height ?? override.baseRows * VANILLA_PORTRAIT_FRAME,
      hdSheetWidth: hdSize.width,
      hdSheetHeight: hdSize.height,
      source: "manual",
    });
  }

  if (baseSize?.width && baseSize.height && !isLikelyHdReplacementBase(baseSize, hdSize)) {
    const { baseColumns, baseRows } = deriveBaseGrid(baseSize);
    return finalizeGrid({
      baseColumns,
      baseRows,
      frameWidth: hdSize.width / baseColumns,
      frameHeight: hdSize.height / baseRows,
      baseSheetWidth: baseSize.width,
      baseSheetHeight: baseSize.height,
      hdSheetWidth: hdSize.width,
      hdSheetHeight: hdSize.height,
      source: "derived",
    });
  }

  return inferPortraitGridFromHdSheet(hdSize);
}

function inferPortraitGridFromHdSheet(hdSize: AtlasSize): PortraitGrid {
  const candidates: PortraitGrid[] = [];

  for (const baseColumns of COMMON_FALLBACK_COLUMNS) {
    if (hdSize.width % baseColumns !== 0) {
      continue;
    }

    const frameWidth = hdSize.width / baseColumns;
    if (frameWidth < 16) {
      continue;
    }

    const baseRows = Math.max(1, Math.round(hdSize.height / frameWidth));
    const frameHeight = hdSize.height / baseRows;
    if (!isInteger(frameHeight) || frameHeight < 16) {
      continue;
    }

    candidates.push(
      finalizeGrid({
        baseColumns,
        baseRows,
        frameWidth,
        frameHeight,
        baseSheetWidth: baseColumns * VANILLA_PORTRAIT_FRAME,
        baseSheetHeight: baseRows * VANILLA_PORTRAIT_FRAME,
        hdSheetWidth: hdSize.width,
        hdSheetHeight: hdSize.height,
        source: "fallback",
        warning: "Base portrait sheet was not found; grid was inferred from common column counts.",
      }),
    );
  }

  return (
    candidates[0] ??
    finalizeGrid({
      baseColumns: 1,
      baseRows: 1,
      frameWidth: hdSize.width,
      frameHeight: hdSize.height,
      baseSheetWidth: hdSize.width,
      baseSheetHeight: hdSize.height,
      hdSheetWidth: hdSize.width,
      hdSheetHeight: hdSize.height,
      source: "fallback",
      warning: "Could not infer a portrait grid; showing the top-left frame only.",
    })
  );
}

function isLikelyHdReplacementBase(baseSize: AtlasSize, hdSize: AtlasSize): boolean {
  return (
    baseSize.width === hdSize.width &&
    baseSize.height === hdSize.height &&
    (baseSize.width > 256 || baseSize.height > 2048)
  );
}

function finalizeGrid(grid: PortraitGrid): PortraitGrid {
  return {
    ...grid,
    baseColumns: Math.max(1, Math.round(grid.baseColumns)),
    baseRows: Math.max(1, Math.round(grid.baseRows)),
    frameWidth: Math.max(1, Math.round(grid.frameWidth)),
    frameHeight: Math.max(1, Math.round(grid.frameHeight)),
  };
}

export function computePortraitFrameRect(
  expressionIndex: number,
  grid: PortraitGrid,
): PortraitFrameRect {
  const index = Number.isFinite(expressionIndex)
    ? Math.max(0, Math.floor(expressionIndex))
    : 0;

  return {
    x: (index % grid.baseColumns) * grid.frameWidth,
    y: Math.floor(index / grid.baseColumns) * grid.frameHeight,
    w: grid.frameWidth,
    h: grid.frameHeight,
    expressionIndex: index,
  };
}

export function getMaxExpressionIndex(grid: PortraitGrid): number {
  return grid.baseColumns * grid.baseRows - 1;
}

export function getPortraitDisplaySize(size: PortraitDisplaySize = "md"): number {
  return PORTRAIT_DISPLAY_SIZES[size];
}

export function buildPortraitFrameStyle(
  sheetUrl: string,
  frame: PortraitFrameRect,
  displaySize: number,
  sheetSize: AtlasSize | null,
): CSSProperties {
  const style: CSSProperties = {
    width: displaySize,
    height: displaySize,
    backgroundImage: `url("${sheetUrl}")`,
    backgroundRepeat: "no-repeat",
    imageRendering: "pixelated",
    display: "block",
  };

  if (!sheetSize?.width || !sheetSize.height) {
    style.backgroundPosition = `${Math.round(-frame.x)}px ${Math.round(-frame.y)}px`;
    return style;
  }

  const renderScale = displaySize / Math.max(1, frame.w);
  style.backgroundPosition = `${Math.round(-frame.x * renderScale)}px ${Math.round(-frame.y * renderScale)}px`;
  style.backgroundSize = `${Math.round(sheetSize.width * renderScale)}px ${Math.round(sheetSize.height * renderScale)}px`;
  return style;
}

function isInteger(value: number): boolean {
  return Math.abs(value - Math.round(value)) < 0.001;
}
