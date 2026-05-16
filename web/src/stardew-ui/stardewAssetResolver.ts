import type { CSSProperties } from "react";
import { useEffect, useState } from "react";
import seedManifestJson from "./stardew-ui-manifest.seed.json";
import type { PortraitGridOverride } from "./portraitFrame";
import { resolvePortraitSource, type ResolvedPortraitSource } from "./portraitSource";
import { inferSpriteKind, type StardewSpriteKind } from "./stardewSpriteTypes";

export type StardewAssetType = "atlas" | "image";
export type StardewSpriteConfidence = "high" | "medium" | "low";

export type StardewAssetDefinition = {
  url: string;
  type?: StardewAssetType;
};

export type StardewRect = {
  x: number;
  y: number;
  w: number;
  h: number;
};

export type StardewNineSlice = {
  top: number;
  right: number;
  bottom: number;
  left: number;
};

export type StardewSpriteDefinition = {
  asset: string;
  rect: StardewRect;
  nineSlice?: StardewNineSlice;
  type?: StardewSpriteKind;
  source?: string;
  confidence?: StardewSpriteConfidence;
  notes?: string;
};

type LegacySpriteDefinition = {
  atlas?: string;
  rect: StardewRect;
  nineSlice?: StardewNineSlice;
  source?: string;
  confidence?: StardewSpriteConfidence;
  notes?: string;
  note?: string;
};

export type StardewManifest = {
  version: number;
  assets?: Record<string, StardewAssetDefinition>;
  sprites?: Record<string, StardewSpriteDefinition>;
  portraits?: Record<string, string>;
  portraitSources?: Record<string, string>;
  portraitGrids?: Record<string, PortraitGridOverride>;
  portraiture?: {
    active?: string | null;
    presets?: Record<string, string>;
    configPath?: string;
  };
  sourceGameDir?: string;
  generatedAt?: string;
  atlases?: Record<string, string>;
};

export type StardewUiManifest = StardewManifest;
export type SpriteRect = StardewRect;
export type NineSliceInsets = StardewNineSlice;
export type ManifestSprite = StardewSpriteDefinition;

export type ResolvedSprite = StardewSpriteDefinition & {
  assetKey: string;
  atlasKey: string;
  atlasUrl: string;
  assetType?: StardewAssetType;
  spriteKind: StardewSpriteKind;
};

export type AtlasSize = {
  width: number;
  height: number;
};

export type StardewAssetResolver = {
  manifest: StardewManifest | null;
  getAsset: (key: string) => StardewAssetDefinition | null;
  getAtlas: (key: string) => string | null;
  getSprite: (key: string) => ResolvedSprite | null;
  getPortrait: (characterName?: string | null, sourceModId?: string | null) => string | null;
  resolvePortrait: (
    characterName?: string | null,
    sourceModId?: string | null,
  ) => ResolvedPortraitSource | null;
};

const LEGACY_GENERATED_MANIFEST_URL = "/generated/stardew-ui/manifest.json";
const LOCAL_MANIFEST_URL = "/generated/stardew-ui/manifest.local.json";

let mergedManifestPromise: Promise<StardewManifest | null> | null = null;
let localManifestPromise: Promise<StardewManifest | null> | null = null;
let legacyManifestPromise: Promise<StardewManifest | null> | null = null;
const atlasSizePromises = new Map<string, Promise<AtlasSize | null>>();
const warnedMessages = new Set<string>();

export function loadSeedManifest(): StardewManifest {
  return normalizeManifest(seedManifestJson as StardewManifest);
}

export async function loadLocalManifest(): Promise<StardewManifest | null> {
  if (!localManifestPromise) {
    localManifestPromise = fetchJsonManifest(LOCAL_MANIFEST_URL, { warnOnMissing: false }).then(
      (manifest) => (manifest ? normalizeManifest(manifest) : null),
    );
  }

  return localManifestPromise;
}

export function mergeStardewManifests(
  seed: StardewManifest | null,
  local?: StardewManifest | null,
): StardewManifest {
  const normalizedSeed = normalizeManifest(seed);
  const normalizedLocal = normalizeManifest(local);

  return {
    ...normalizedSeed,
    ...normalizedLocal,
    version: normalizedLocal.version || normalizedSeed.version || 1,
    assets: {
      ...(normalizedSeed.assets ?? {}),
      ...(normalizedLocal.assets ?? {}),
    },
    sprites: {
      ...(normalizedSeed.sprites ?? {}),
      ...(normalizedLocal.sprites ?? {}),
    },
    portraits: {
      ...(normalizedSeed.portraits ?? {}),
      ...(normalizedLocal.portraits ?? {}),
    },
    portraitSources: {
      ...(normalizedSeed.portraitSources ?? {}),
      ...(normalizedLocal.portraitSources ?? {}),
    },
    portraitGrids: {
      ...(normalizedSeed.portraitGrids ?? {}),
      ...(normalizedLocal.portraitGrids ?? {}),
    },
    portraiture: {
      ...(normalizedSeed.portraiture ?? {}),
      ...(normalizedLocal.portraiture ?? {}),
      presets: {
        ...(normalizedSeed.portraiture?.presets ?? {}),
        ...(normalizedLocal.portraiture?.presets ?? {}),
      },
    },
  };
}

export async function loadMergedStardewManifest(): Promise<StardewManifest | null> {
  if (!mergedManifestPromise) {
    mergedManifestPromise = Promise.all([
      loadGeneratedManifestShell(),
      loadLocalManifest(),
    ]).then(([generated, local]) => mergeStardewManifests(mergeStardewManifests(loadSeedManifest(), generated), local));
  }

  return mergedManifestPromise;
}

export function createStardewAssetResolver(
  manifest: StardewManifest | null,
): StardewAssetResolver {
  const normalizedManifest = normalizeManifest(manifest);

  return {
    manifest: normalizedManifest,
    getAsset: (key) => resolveStardewAsset(key, normalizedManifest),
    getAtlas: (key) => resolveStardewAsset(key, normalizedManifest)?.url ?? null,
    getSprite: (key) => resolveStardewSprite(key, normalizedManifest),
    getPortrait: (characterName, sourceModId) => {
      const portraits = normalizedManifest.portraits;
      const normalizedName = normalizeAssetKey(characterName);
      if (!portraits || !normalizedName) {
        return null;
      }

      const modKey = sourceModId ? `${normalizeAssetKey(sourceModId)}/${normalizedName}` : null;
      const rawModKey = sourceModId && characterName ? `${sourceModId}/${characterName}` : null;
      const candidates = [
        rawModKey,
        modKey,
        characterName ?? null,
        normalizedName,
        normalizedName.toLowerCase(),
      ].filter((value): value is string => Boolean(value));

      for (const candidate of candidates) {
        const match = portraits[candidate];
        if (match) {
          return match;
        }
      }

      return null;
    },
    resolvePortrait: (characterName, sourceModId) =>
      resolvePortraitSource(characterName, normalizedManifest, sourceModId),
  };
}

export function useStardewAssetResolver(): StardewAssetResolver {
  const [manifest, setManifest] = useState<StardewManifest | null>(() => loadSeedManifest());

  useEffect(() => {
    let cancelled = false;
    void loadMergedStardewManifest().then((loaded) => {
      if (!cancelled) {
        setManifest(loaded);
      }
    });

    return () => {
      cancelled = true;
    };
  }, []);

  return createStardewAssetResolver(manifest);
}

export function resolveStardewAsset(
  assetKey: string,
  manifest: StardewManifest | null = loadSeedManifest(),
): StardewAssetDefinition | null {
  const asset = normalizeManifest(manifest).assets?.[assetKey] ?? null;
  if (!asset) {
    warnOnce(`Missing Stardew UI asset '${assetKey}'.`);
  }

  return asset;
}

export function resolveStardewSprite(
  spriteKey: string,
  manifest: StardewManifest | null = loadSeedManifest(),
): ResolvedSprite | null {
  const normalizedManifest = normalizeManifest(manifest);
  const sprite = normalizedManifest.sprites?.[spriteKey];
  if (!sprite) {
    warnOnce(`Missing Stardew UI sprite '${spriteKey}'.`);
    return null;
  }

  const asset = resolveStardewAsset(sprite.asset, normalizedManifest);
  if (!asset) {
    return null;
  }

  return {
    ...sprite,
    assetKey: sprite.asset,
    atlasKey: sprite.asset,
    atlasUrl: normalizePublicAssetUrl(asset.url),
    assetType: asset.type,
    spriteKind: inferSpriteKind(spriteKey, sprite.type),
  };
}

export function getSpriteUrl(
  spriteKey: string,
  manifest: StardewManifest | null = loadSeedManifest(),
): string | null {
  return resolveStardewSprite(spriteKey, manifest)?.atlasUrl ?? null;
}

export function getSpriteRect(
  spriteKey: string,
  manifest: StardewManifest | null = loadSeedManifest(),
): StardewRect | null {
  return resolveStardewSprite(spriteKey, manifest)?.rect ?? null;
}

export function getSpriteCssVariables(sprite: ResolvedSprite): Record<string, string> {
  return {
    "--stardew-atlas": `url("${sprite.atlasUrl}")`,
    "--stardew-sprite-x": `${sprite.rect.x}px`,
    "--stardew-sprite-y": `${sprite.rect.y}px`,
    "--stardew-sprite-w": `${sprite.rect.w}px`,
    "--stardew-sprite-h": `${sprite.rect.h}px`,
  };
}

export function computeSpriteRenderScale(
  sprite: ResolvedSprite,
  {
    scale = 2,
    size,
  }: {
    scale?: number;
    size?: number;
  } = {},
): number {
  if (size) {
    return size / Math.max(sprite.rect.w, sprite.rect.h);
  }

  return scale;
}

/**
 * CSS atlas crop. When atlas natural size is unknown, uses 1x native coordinates only
 * (no background-size). Caller may wrap with transform to upscale visually.
 */
export function buildSpriteBackgroundStyle(
  sprite: ResolvedSprite,
  {
    scale = 2,
    size,
    atlasSize,
  }: {
    scale?: number;
    size?: number;
    atlasSize?: AtlasSize | null;
  } = {},
): CSSProperties {
  const hasAtlasDimensions =
    atlasSize != null && atlasSize.width > 0 && atlasSize.height > 0;
  const renderScale = hasAtlasDimensions
    ? computeSpriteRenderScale(sprite, { scale, size })
    : 1;
  const width = Math.max(1, Math.round(sprite.rect.w * renderScale));
  const height = Math.max(1, Math.round(sprite.rect.h * renderScale));

  const style: CSSProperties = {
    width,
    height,
    backgroundImage: `url("${sprite.atlasUrl}")`,
    backgroundRepeat: "no-repeat",
    imageRendering: "pixelated",
  };

  if (hasAtlasDimensions) {
    style.backgroundPosition = `${Math.round(-sprite.rect.x * renderScale)}px ${Math.round(-sprite.rect.y * renderScale)}px`;
    style.backgroundSize = `${Math.round(atlasSize.width * renderScale)}px ${Math.round(atlasSize.height * renderScale)}px`;
  } else {
    style.backgroundPosition = `${-sprite.rect.x}px ${-sprite.rect.y}px`;
  }

  return style;
}

export function useAtlasNaturalSize(atlasUrl?: string | null): AtlasSize | null {
  const [size, setSize] = useState<AtlasSize | null>(null);

  useEffect(() => {
    if (!atlasUrl) {
      setSize(null);
      return;
    }

    let cancelled = false;
    void loadAtlasNaturalSize(atlasUrl).then((loaded) => {
      if (!cancelled) {
        setSize(loaded);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [atlasUrl]);

  return size;
}

async function loadGeneratedManifestShell(): Promise<StardewManifest | null> {
  if (!legacyManifestPromise) {
    legacyManifestPromise = fetchJsonManifest(LEGACY_GENERATED_MANIFEST_URL, {
      warnOnMissing: false,
    }).then((manifest) => {
      const normalized = normalizeManifest(manifest);
      return {
        version: normalized.version || 1,
        sourceGameDir: normalized.sourceGameDir,
        generatedAt: normalized.generatedAt,
        assets: normalized.assets,
        portraits: normalized.portraits,
        portraitSources: normalized.portraitSources,
        portraitGrids: normalized.portraitGrids,
        portraiture: normalized.portraiture,
      };
    });
  }

  return legacyManifestPromise;
}

async function fetchJsonManifest(
  url: string,
  { warnOnMissing }: { warnOnMissing: boolean },
): Promise<StardewManifest | null> {
  try {
    const response = await fetch(url, { cache: "no-store" });
    if (!response.ok) {
      if (warnOnMissing || response.status !== 404) {
        warnOnce(`Could not load Stardew UI manifest '${url}' (${response.status}).`);
      }
      return null;
    }

    return (await response.json()) as StardewManifest;
  } catch (error) {
    warnOnce(`Could not load Stardew UI manifest '${url}': ${String(error)}`);
    return null;
  }
}

async function loadAtlasNaturalSize(atlasUrl: string): Promise<AtlasSize | null> {
  if (!atlasSizePromises.has(atlasUrl)) {
    atlasSizePromises.set(
      atlasUrl,
      new Promise((resolve) => {
        const image = new Image();
        image.onload = () =>
          resolve({
            width: image.naturalWidth,
            height: image.naturalHeight,
          });
        image.onerror = () => {
          warnOnce(`Could not load Stardew UI atlas '${atlasUrl}'.`);
          resolve(null);
        };
        image.src = atlasUrl;
      }),
    );
  }

  return atlasSizePromises.get(atlasUrl) ?? null;
}

function normalizeManifest(manifest?: StardewManifest | null): StardewManifest {
  if (!manifest) {
    return { version: 1 };
  }

  const assets = { ...(manifest.assets ?? {}) };
  for (const [atlasKey, atlasUrl] of Object.entries(manifest.atlases ?? {})) {
    const assetKey = legacyAtlasKeyToAssetKey(atlasKey);
    if (assetKey && !assets[assetKey]) {
      assets[assetKey] = { url: normalizePublicAssetUrl(atlasUrl), type: "atlas" };
    }
  }

  for (const [key, asset] of Object.entries(assets)) {
    assets[key] = {
      ...asset,
      url: normalizePublicAssetUrl(asset.url),
    };
  }

  const sprites: Record<string, StardewSpriteDefinition> = {};
  for (const [key, sprite] of Object.entries((manifest.sprites ?? {}) as Record<string, StardewSpriteDefinition | LegacySpriteDefinition>)) {
    const normalized = normalizeSprite(sprite);
    if (normalized) {
      sprites[key] = normalized;
    }
  }

  return {
    ...manifest,
    version: manifest.version || 1,
    assets,
    sprites,
  };
}

function normalizeSprite(
  sprite: StardewSpriteDefinition | LegacySpriteDefinition,
): StardewSpriteDefinition | null {
  if ("asset" in sprite && sprite.asset) {
    return sprite;
  }

  const legacyAsset = legacyAtlasKeyToAssetKey(sprite.atlas);
  if (!legacyAsset) {
    return null;
  }

  return {
    asset: legacyAsset,
    rect: sprite.rect,
    nineSlice: sprite.nineSlice,
    source: sprite.source,
    confidence: sprite.confidence,
    notes: sprite.notes ?? sprite.note,
  };
}

function legacyAtlasKeyToAssetKey(value?: string): string | null {
  switch (value) {
    case "cursors":
      return "LooseSprites/Cursors";
    case "cursors2":
      return "LooseSprites/Cursors2";
    case "cursors_1_6":
      return "LooseSprites/Cursors_1_6";
    case "textBox":
      return "LooseSprites/textBox";
    case "chatBox":
      return "LooseSprites/chatBox";
    case "billboard":
      return "LooseSprites/Billboard";
    case "junimoNote":
      return "LooseSprites/JunimoNote";
    case "specialOrdersBoard":
      return "LooseSprites/SpecialOrdersBoard";
    case "menuTiles":
      return "Maps/MenuTiles";
    case "menuTilesUncolored":
      return "Maps/MenuTilesUncolored";
    default:
      return value?.includes("/") ? value : null;
  }
}

function normalizeAssetKey(value?: string | null): string | null {
  const normalized = value
    ?.trim()
    .replace(/[^A-Za-z0-9_.-]+/g, "_")
    .replace(/^_+|_+$/g, "");

  return normalized || null;
}

/** Browser URL for files under web/public — never includes /public. */
export function normalizePublicAssetUrl(url: string): string {
  const trimmed = url.trim();
  if (!trimmed) {
    return trimmed;
  }

  if (trimmed.startsWith("/public/")) {
    return trimmed.replace(/^\/public/, "");
  }

  if (trimmed.startsWith("public/")) {
    return `/${trimmed.slice("public/".length)}`;
  }

  if (trimmed.startsWith("/")) {
    return trimmed;
  }

  return `/${trimmed}`;
}

export async function probeAssetUrl(url: string): Promise<{
  url: string;
  ok: boolean;
  status: number | null;
  contentType: string | null;
}> {
  const normalized = normalizePublicAssetUrl(url);
  try {
    const response = await fetch(normalized, { method: "HEAD", cache: "no-store" });
    return {
      url: normalized,
      ok: response.ok,
      status: response.status,
      contentType: response.headers.get("content-type"),
    };
  } catch {
    try {
      const response = await fetch(normalized, { method: "GET", cache: "no-store" });
      return {
        url: normalized,
        ok: response.ok,
        status: response.status,
        contentType: response.headers.get("content-type"),
      };
    } catch {
      return { url: normalized, ok: false, status: null, contentType: null };
    }
  }
}

function warnOnce(message: string): void {
  if (warnedMessages.has(message)) {
    return;
  }

  warnedMessages.add(message);
  console.warn(message);
}
