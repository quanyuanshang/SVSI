import type { PortraitGridOverride } from "./portraitFrame";
import type { StardewManifest } from "./stardewAssetResolver";

export type ResolvedPortraitSource = {
  characterName: string;
  hdUrl: string;
  baseUrl: string;
  gridOverride: PortraitGridOverride | null;
  sourceLabel: string;
};

const VANILLA_PORTRAIT_PREFIX = "/generated/stardew-ui/Portraits/";

export function buildVanillaPortraitUrl(characterName: string): string {
  return `${VANILLA_PORTRAIT_PREFIX}${normalizeAssetKey(characterName) ?? characterName}.png`;
}

export function resolvePortraitSource(
  characterName: string | null | undefined,
  manifest: StardewManifest | null,
  sourceModId?: string | null,
): ResolvedPortraitSource | null {
  const name = characterName?.trim();
  if (!name) {
    return null;
  }

  const baseUrl = resolveBasePortraitUrl(name, manifest);
  const activeSet = manifest?.portraiture?.presets?.[name] ?? manifest?.portraiture?.active ?? null;
  const portraitureSource = activeSet
    ? resolvePortraitureSetSource(name, activeSet, manifest)
    : null;
  const hdSource =
    portraitureSource ??
    resolvePortraitSourceRecord(name, manifest, sourceModId) ??
    { url: baseUrl, label: "vanilla" };

  return {
    characterName: name,
    hdUrl: hdSource.url,
    baseUrl,
    gridOverride: resolvePortraitGridOverride(name, manifest, sourceModId, hdSource.url),
    sourceLabel: hdSource.label,
  };
}

function resolveBasePortraitUrl(characterName: string, manifest: StardewManifest | null): string {
  const portraits = manifest?.portraits;
  for (const key of portraitLookupKeys(characterName)) {
    const url = portraits?.[key];
    if (url && isVanillaPortraitUrl(url)) {
      return url;
    }
  }

  return buildVanillaPortraitUrl(characterName);
}

function resolvePortraitureSetSource(
  characterName: string,
  setName: string,
  manifest: StardewManifest | null,
): { url: string; label: string } | null {
  const sources = manifest?.portraitSources;
  if (!sources) {
    return null;
  }

  for (const nameKey of portraitLookupKeys(characterName)) {
    for (const key of [
      `Portraiture/${setName}/${nameKey}`,
      `Portraiture/Portraits/${setName}/${nameKey}`,
      `${setName}/${nameKey}`,
    ]) {
      const url = sources[key];
      if (url) {
        return { url, label: `Portraiture: ${setName}` };
      }
    }
  }

  return null;
}

function resolvePortraitSourceRecord(
  characterName: string,
  manifest: StardewManifest | null,
  sourceModId?: string | null,
): { url: string; label: string } | null {
  const sources = manifest?.portraitSources;
  if (!sources) {
    return null;
  }

  for (const key of portraitLookupKeys(characterName, sourceModId)) {
    const url = sources[key];
    if (url) {
      return { url, label: key.includes("/") ? key.split("/")[0] : "mod portrait" };
    }
  }

  return null;
}

function resolvePortraitGridOverride(
  characterName: string,
  manifest: StardewManifest | null,
  sourceModId?: string | null,
  hdUrl?: string | null,
): PortraitGridOverride | null {
  const grids = manifest?.portraitGrids;
  if (!grids) {
    return null;
  }

  for (const key of [...portraitLookupKeys(characterName, sourceModId), hdUrl ?? ""]) {
    if (key && grids[key]) {
      return grids[key];
    }
  }

  return null;
}

function portraitLookupKeys(characterName: string, sourceModId?: string | null): string[] {
  const normalizedName = normalizeAssetKey(characterName);
  const normalizedModId = normalizeAssetKey(sourceModId);
  return [
    sourceModId && characterName ? `${sourceModId}/${characterName}` : null,
    normalizedModId && normalizedName ? `${normalizedModId}/${normalizedName}` : null,
    characterName,
    normalizedName,
    normalizedName?.toLowerCase(),
  ]
    .filter((value): value is string => Boolean(value))
    .filter((value, index, array) => array.indexOf(value) === index);
}

function normalizeAssetKey(value?: string | null): string | null {
  const normalized = value
    ?.trim()
    .replace(/[^A-Za-z0-9_.-]+/g, "_")
    .replace(/^_+|_+$/g, "");

  return normalized || null;
}

function isVanillaPortraitUrl(url: string): boolean {
  const rest = url.replace(/\\/g, "/").slice(VANILLA_PORTRAIT_PREFIX.length);
  return url.startsWith(VANILLA_PORTRAIT_PREFIX) && !rest.includes("/");
}
