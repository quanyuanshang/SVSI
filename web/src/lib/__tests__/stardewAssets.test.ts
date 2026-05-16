import { describe, expect, it } from "vitest";
import {
  buildSpriteBackgroundStyle,
  createStardewAssetResolver,
  mergeStardewManifests,
  resolvePortraitSource,
  type StardewUiManifest,
} from "../stardewAssets";

const manifest: StardewUiManifest = {
  version: 1,
  sourceGameDir: "D:/Steam/Stardew Valley",
  generatedAt: "2026-05-17T00:00:00.000Z",
  assets: {
    "LooseSprites/Cursors": {
      url: "/generated/stardew-ui/LooseSprites/Cursors.png",
      type: "atlas",
    },
    "LooseSprites/textBox": {
      url: "/generated/stardew-ui/LooseSprites/textBox.png",
      type: "atlas",
    },
  },
  sprites: {
    "ui.panel.default": {
      asset: "LooseSprites/Cursors",
      rect: { x: 0, y: 0, w: 64, h: 64 },
      nineSlice: { top: 12, right: 12, bottom: 12, left: 12 },
      source: "official-wiki:Modding:Shops",
      confidence: "high",
      notes: "Test fixture",
    },
    "icon.lock": {
      asset: "LooseSprites/Cursors",
      rect: { x: 16, y: 16, w: 16, h: 16 },
      type: "icon",
      source: "manual:/stardew-assets-debug",
      confidence: "medium",
      notes: "Test fixture",
    },
  },
  portraits: {
    Sebastian: "/generated/stardew-ui/Portraits/Sebastian.png",
    "FlashShifter.StardewValleyExpanded/Lance":
      "/generated/stardew-ui/Portraits/FlashShifter.StardewValleyExpanded/Lance.png",
  },
  portraitSources: {
    "Portraiture/Portraits7/Sebastian": "/generated/stardew-ui/Portraiture/Portraits7/Sebastian.png",
  },
  portraiture: {
    active: "Portraits7",
    presets: {
      Sebastian: "Portraits7",
    },
  },
};

describe("stardewAssets", () => {
  it("resolves sprite keys through asset names", () => {
    const resolver = createStardewAssetResolver(manifest);

    expect(resolver.getSprite("icon.lock")).toMatchObject({
      asset: "LooseSprites/Cursors",
      assetKey: "LooseSprites/Cursors",
      atlasKey: "LooseSprites/Cursors",
      atlasUrl: "/generated/stardew-ui/LooseSprites/Cursors.png",
      assetType: "atlas",
      spriteKind: "icon",
      rect: { x: 16, y: 16, w: 16, h: 16 },
      source: "manual:/stardew-assets-debug",
      confidence: "medium",
      notes: "Test fixture",
    });
  });

  it("merges seed and local manifests with local sprites taking priority", () => {
    const merged = mergeStardewManifests(manifest, {
      version: 1,
      assets: {
        "LooseSprites/Cursors2": {
          url: "/generated/stardew-ui/LooseSprites/Cursors2.png",
          type: "atlas",
        },
      },
      sprites: {
        "icon.lock": {
          asset: "LooseSprites/Cursors2",
          rect: { x: 1, y: 2, w: 3, h: 4 },
          source: "manual:/stardew-assets-debug",
          confidence: "medium",
          notes: "Local override",
        },
      },
    });

    expect(merged.assets?.["LooseSprites/Cursors"]?.url).toBe(
      "/generated/stardew-ui/LooseSprites/Cursors.png",
    );
    expect(merged.assets?.["LooseSprites/Cursors2"]?.url).toBe(
      "/generated/stardew-ui/LooseSprites/Cursors2.png",
    );
    expect(merged.sprites?.["icon.lock"]).toMatchObject({
      asset: "LooseSprites/Cursors2",
      rect: { x: 1, y: 2, w: 3, h: 4 },
      notes: "Local override",
    });
  });

  it("returns null when sprite or atlas data is missing", () => {
    const resolver = createStardewAssetResolver(manifest);

    expect(resolver.getSprite("icon.missing")).toBeNull();
    expect(
      createStardewAssetResolver({
        ...manifest,
        sprites: {
          broken: {
            asset: "missing",
            rect: { x: 0, y: 0, w: 1, h: 1 },
            source: "manual:/stardew-assets-debug",
            confidence: "low",
            notes: "Broken fixture",
          },
        },
      }).getSprite("broken"),
    ).toBeNull();
  });

  it("resolves portraits by mod-qualified key before plain character name", () => {
    const resolver = createStardewAssetResolver(manifest);

    expect(resolver.getPortrait("Lance", "FlashShifter.StardewValleyExpanded")).toBe(
      "/generated/stardew-ui/Portraits/FlashShifter.StardewValleyExpanded/Lance.png",
    );
    expect(resolver.getPortrait("Sebastian", "Some.Other.Mod")).toBe(
      "/generated/stardew-ui/Portraits/Sebastian.png",
    );
    expect(resolver.getPortrait("Missing")).toBeNull();
  });

  it("prefers the active Portraiture set and keeps the vanilla sheet as base", () => {
    expect(resolvePortraitSource("Sebastian", manifest)).toMatchObject({
      characterName: "Sebastian",
      hdUrl: "/generated/stardew-ui/Portraiture/Portraits7/Sebastian.png",
      baseUrl: "/generated/stardew-ui/Portraits/Sebastian.png",
      sourceLabel: "Portraiture: Portraits7",
    });
  });

  it("builds a scaled background style for atlas rect sprites", () => {
    const resolver = createStardewAssetResolver(manifest);
    const sprite = resolver.getSprite("icon.lock");

    expect(sprite).not.toBeNull();
    expect(
      buildSpriteBackgroundStyle(sprite!, {
        scale: 3,
        atlasSize: { width: 512, height: 512 },
      }),
    ).toMatchObject({
      width: 48,
      height: 48,
      backgroundImage: 'url("/generated/stardew-ui/LooseSprites/Cursors.png")',
      backgroundPosition: "-48px -48px",
      backgroundSize: "1536px 1536px",
    });
  });

  it("uses native 1x coordinates when atlas natural size is not available", () => {
    const resolver = createStardewAssetResolver(manifest);
    const sprite = resolver.getSprite("icon.lock");

    expect(sprite).not.toBeNull();
    const style = buildSpriteBackgroundStyle(sprite!, {
      scale: 2,
      atlasSize: null,
    });
    expect(style).toMatchObject({
      width: 16,
      height: 16,
      backgroundPosition: "-16px -16px",
      backgroundRepeat: "no-repeat",
    });
    expect(style.backgroundSize).toBeUndefined();
  });
});
