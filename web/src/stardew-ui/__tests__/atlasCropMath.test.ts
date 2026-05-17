import { describe, expect, it } from "vitest";
import { getAtlasPoint, normalizeRect } from "../atlasCropMath";

describe("atlasCropMath", () => {
  it("normalizes drag rectangles with positive width and height", () => {
    expect(normalizeRect(10, 20, 30, 40)).toEqual({ x: 10, y: 20, w: 20, h: 20 });
    expect(normalizeRect(30, 40, 10, 20)).toEqual({ x: 10, y: 20, w: 20, h: 20 });
    expect(normalizeRect(5, 5, 5, 5)).toEqual({ x: 5, y: 5, w: 1, h: 1 });
  });

  it("maps client coordinates to atlas pixels using natural size", () => {
    const image = {
      naturalWidth: 100,
      naturalHeight: 50,
      getBoundingClientRect: () => ({
        left: 0,
        top: 0,
        width: 50,
        height: 25,
        right: 50,
        bottom: 25,
        x: 0,
        y: 0,
        toJSON: () => ({}),
      }),
    } as HTMLImageElement;

    expect(getAtlasPoint(image, 25, 12)).toEqual({ x: 50, y: 24 });
    expect(getAtlasPoint(image, 0, 0)).toEqual({ x: 0, y: 0 });
    expect(getAtlasPoint(image, 50, 25)).toEqual({ x: 100, y: 50 });
  });
});
