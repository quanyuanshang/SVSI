import { describe, expect, it } from "vitest";
import {
  buildPortraitFrameStyle,
  computePortraitFrameRect,
  deriveBaseGrid,
  derivePortraitGrid,
  VANILLA_PORTRAIT_FRAME,
} from "../portraitFrame";

describe("portraitFrame", () => {
  it("derives the base grid from vanilla 64px portrait cells", () => {
    expect(deriveBaseGrid({ width: 128, height: 320 })).toEqual({
      baseColumns: 2,
      baseRows: 5,
    });
    expect(VANILLA_PORTRAIT_FRAME).toBe(64);
  });

  it("derives HD frame size from base grid instead of assuming 64px", () => {
    const grid = derivePortraitGrid(
      { width: 128, height: 320 },
      { width: 256, height: 640 },
    );
    expect(grid).toMatchObject({
      baseColumns: 2,
      baseRows: 5,
      frameWidth: 128,
      frameHeight: 128,
      source: "derived",
    });
  });

  it("maps expressionIndex to a rect on the derived HD grid", () => {
    const grid = derivePortraitGrid(
      { width: 128, height: 320 },
      { width: 256, height: 640 },
    );
    const frame = computePortraitFrameRect(3, grid!);
    expect(frame).toEqual({
      x: 128,
      y: 128,
      w: 128,
      h: 128,
      expressionIndex: 3,
    });
  });

  it("falls back to the largest valid common grid when the base sheet is missing", () => {
    const grid = derivePortraitGrid(null, { width: 128, height: 320 });
    expect(grid).toMatchObject({
      baseColumns: 2,
      baseRows: 5,
      frameWidth: 64,
      frameHeight: 64,
      source: "fallback",
    });
  });

  it("does not force HD-only Portraiture sheets into 64px cells", () => {
    const grid = derivePortraitGrid(null, { width: 512, height: 2304 });
    expect(grid).toMatchObject({
      baseColumns: 2,
      baseRows: 9,
      frameWidth: 256,
      frameHeight: 256,
      source: "fallback",
    });
  });

  it("uses Portraiture 2-column grid when the CP base row count does not match HD height (SDS Sariel)", () => {
    const grid = derivePortraitGrid(
      { width: 512, height: 3072 },
      { width: 512, height: 2048 },
    );
    expect(grid).toMatchObject({
      baseColumns: 2,
      baseRows: 8,
      frameWidth: 256,
      frameHeight: 256,
      source: "fallback",
    });
  });

  it("infers a multi-frame grid when the vanilla base is only a single 64px tile", () => {
    const grid = derivePortraitGrid({ width: 64, height: 64 }, { width: 128, height: 192 });
    expect(grid).toMatchObject({
      baseColumns: 2,
      baseRows: 3,
      frameWidth: 64,
      frameHeight: 64,
      source: "fallback",
    });
  });

  it("ignores a replaced HD base sheet with the same dimensions as the HD sheet", () => {
    const grid = derivePortraitGrid(
      { width: 512, height: 4096 },
      { width: 512, height: 4096 },
    );
    expect(grid).toMatchObject({
      baseColumns: 2,
      baseRows: 16,
      frameWidth: 256,
      frameHeight: 256,
      source: "fallback",
    });
  });

  it("builds scaled CSS crop on the display box using the derived frame", () => {
    const grid = derivePortraitGrid(
      { width: 128, height: 320 },
      { width: 256, height: 640 },
    );
    const frame = computePortraitFrameRect(1, grid!);
    const style = buildPortraitFrameStyle(
      "/Portraits/Sebastian.png",
      frame,
      72,
      { width: 256, height: 640 },
    );

    expect(style.backgroundImage).toBe('url("/Portraits/Sebastian.png")');
    expect(style.backgroundPosition).toBe("-72px 0px");
    expect(style.backgroundSize).toBe("144px 360px");
    expect(style.width).toBe(72);
    expect(style.height).toBe(72);
  });
});
