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

/** NPC id → vanilla portrait sheet file name (when they differ). */
const PORTRAIT_ASSET_ALIASES: Record<string, string> = {
  Leo: "ParrotBoy",
  LeoMainland: "ParrotBoy",
};

export function resolvePortraitAssetName(characterName: string): string {
  return PORTRAIT_ASSET_ALIASES[characterName] ?? characterName;
}

export function buildVanillaPortraitUrl(characterName: string): string {
  const assetName = resolvePortraitAssetName(characterName);
  return `${VANILLA_PORTRAIT_PREFIX}${normalizeAssetKey(assetName) ?? assetName}.png`;
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

  const assetName = resolvePortraitAssetName(name);
  const baseUrl = resolveBasePortraitUrl(assetName, manifest, name);
  const activeSet =
    manifest?.portraiture?.presets?.[name] ??
    manifest?.portraiture?.presets?.[assetName] ??
    manifest?.portraiture?.active ??
    null;
  const portraitureSource = activeSet
    ? resolvePortraitureSetSource(assetName, activeSet, manifest)
    : null;
  const hdSource =
    portraitureSource ??
    resolvePortraitSourceRecord(assetName, manifest, sourceModId) ??
    resolvePortraitSourceRecord(name, manifest, sourceModId) ??
    resolveUniqueModPortraitSuffix(assetName, manifest) ??
    resolveUniqueModPortraitSuffix(name, manifest) ??
    { url: baseUrl, label: "vanilla" };

  return {
    characterName: name,
    hdUrl: hdSource.url,
    baseUrl,
    gridOverride: resolvePortraitGridOverride(
      assetName,
      manifest,
      sourceModId,
      hdSource.url,
    ),
    sourceLabel: hdSource.label,
  };
}

function resolveBasePortraitUrl(
  assetName: string,
  manifest: StardewManifest | null,
  displayName?: string,
): string {
  const portraits = manifest?.portraits;
  for (const key of portraitLookupKeys(assetName, null, displayName)) {
    const url = portraits?.[key];
    if (url && isVanillaPortraitUrl(url)) {
      return url;
    }
  }

  return buildVanillaPortraitUrl(assetName);
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

/** When only one mod-specific portraitSources key matches the character suffix. */
function resolveUniqueModPortraitSuffix(
  characterName: string,
  manifest: StardewManifest | null,
): { url: string; label: string } | null {
  const sources = manifest?.portraitSources;
  if (!sources) {
    return null;
  }

  const suffixKeys = new Set(
    portraitLookupKeys(characterName).filter((key) => !key.includes("/")),
  );
  const matches: Array<{ key: string; url: string }> = [];

  for (const [key, url] of Object.entries(sources)) {
    const slash = key.indexOf("/");
    if (slash < 0 || key.startsWith("Portraiture/")) {
      continue;
    }

    const suffix = key.slice(slash + 1);
    if (suffixKeys.has(suffix)) {
      matches.push({ key, url });
    }
  }

  if (matches.length !== 1) {
    return null;
  }

  const modId = matches[0].key.slice(0, matches[0].key.indexOf("/"));
  return { url: matches[0].url, label: modId };
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

function portraitLookupKeys(
  characterName: string,
  sourceModId?: string | null,
  displayName?: string | null,
): string[] {
  const normalizedName = normalizeAssetKey(characterName);
  const normalizedModId = normalizeAssetKey(sourceModId);
  const normalizedDisplayName = displayName ? normalizeAssetKey(displayName) : null;
  return [
    sourceModId && characterName ? `${sourceModId}/${characterName}` : null,
    normalizedModId && normalizedName ? `${normalizedModId}/${normalizedName}` : null,
    sourceModId && displayName ? `${sourceModId}/${displayName}` : null,
    normalizedModId && normalizedDisplayName
      ? `${normalizedModId}/${normalizedDisplayName}`
      : null,
    characterName,
    normalizedName,
    normalizedName?.toLowerCase(),
    displayName,
    normalizedDisplayName,
    normalizedDisplayName?.toLowerCase(),
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
