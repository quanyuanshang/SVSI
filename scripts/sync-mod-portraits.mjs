#!/usr/bin/env node
/**
 * Sync Portraiture and Content Patcher portrait sheets into web/public.
 *
 * This writes portrait sources only. Frame size is derived in the browser from
 * the vanilla 64x64 portrait grid plus the selected HD sheet dimensions.
 */

import {
  copyFileSync,
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDir, "..");
const webPublic = join(projectRoot, "web", "public", "generated", "stardew-ui");
const portraitureOutRoot = join(webPublic, "Portraiture");
const cpOutRoot = join(webPublic, "Portraits");
const localManifestPath = join(webPublic, "manifest.local.json");
const DEFAULT_GAME_DIR = "D:/SteamLibrary/steamapps/common/Stardew Valley";

const args = parseArgs(process.argv.slice(2));
const gameDir = resolve(args["game-dir"] ?? DEFAULT_GAME_DIR);
const modsDir = resolve(args["mods-dir"] ?? join(gameDir, "Mods"));
const portraitureDir = resolve(args["portraiture-dir"] ?? join(modsDir, "Portraiture"));

function parseArgs(argv) {
  const parsed = {};
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (!arg.startsWith("--")) continue;
    const key = arg.slice(2);
    const next = argv[index + 1];
    if (next && !next.startsWith("--")) {
      parsed[key] = next;
      index += 1;
    } else {
      parsed[key] = "true";
    }
  }
  return parsed;
}

function readJson(filePath) {
  try {
    return JSON.parse(stripJsonComments(readFileSync(filePath, "utf-8")));
  } catch {
    return null;
  }
}

function stripJsonComments(text) {
  return text
    .replace(/^\uFEFF/, "")
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/(^|[^:])\/\/.*$/gm, "$1")
    .replace(/,\s*([}\]])/g, "$1");
}

function safeSegment(value) {
  return String(value)
    .trim()
    .replace(/[^A-Za-z0-9_.-]+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function toPublicUrl(root, filePath) {
  return `/generated/stardew-ui/${relative(webPublic, filePath).replace(/\\/g, "/")}`;
}

function copyPortrait(source, output) {
  mkdirSync(dirname(output), { recursive: true });
  copyFileSync(source, output);
  return toPublicUrl(webPublic, output);
}

function collectJsonFiles(dir, result = []) {
  if (!existsSync(dir)) return result;

  for (const entry of readdirSync(dir)) {
    const fullPath = join(dir, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) {
      collectJsonFiles(fullPath, result);
    } else if (entry.toLowerCase().endsWith(".json")) {
      result.push(fullPath);
    }
  }

  return result;
}

function loadPortraitureConfig() {
  const config = readJson(join(portraitureDir, "config.json"));
  const presets = {};
  for (const preset of config?.presets?.Presets ?? []) {
    if (preset.Character && preset.Portraits) {
      presets[preset.Character] = preset.Portraits;
    }
  }

  return {
    active: config?.active ?? null,
    presets,
    configPath: join(portraitureDir, "config.json"),
  };
}

function syncPortraitureSources(manifest) {
  const portraitsRoot = join(portraitureDir, "Portraits");
  const config = loadPortraitureConfig();
  manifest.portraiture = config;

  if (!existsSync(portraitsRoot)) {
    return [];
  }

  const copied = [];
  for (const setName of readdirSync(portraitsRoot)) {
    const setDir = join(portraitsRoot, setName);
    if (!statSync(setDir).isDirectory()) continue;

    for (const fileName of readdirSync(setDir)) {
      if (!fileName.toLowerCase().endsWith(".png")) continue;
      const characterName = fileName.replace(/\.png$/i, "");
      const source = join(setDir, fileName);
      const output = join(portraitureOutRoot, safeSegment(setName), `${safeSegment(characterName)}.png`);
      const url = copyPortrait(source, output);
      manifest.portraitSources[`Portraiture/${setName}/${characterName}`] = url;
      manifest.portraitSources[`Portraiture/${safeSegment(setName)}/${safeSegment(characterName)}`] = url;
      copied.push(`Portraiture/${setName}/${characterName}`);
    }
  }

  return copied;
}

function isBasePortraitFromFile(characterName, fromFile) {
  const normalized = fromFile.replace(/\\/g, "/");
  return (
    normalized.endsWith(`/${characterName}.png`) ||
    normalized.endsWith(`/Portraits/${characterName}/${characterName}.png`)
  );
}

/** Regex fallback for JSONC content packs (e.g. SVE NPC files with comments / trailing commas). */
function extractPortraitLoadsFromText(text) {
  const stripped = stripJsonComments(text);
  const loads = new Map();
  const pattern =
    /"Action"\s*:\s*"Load"[\s\S]*?"Target"\s*:\s*"Portraits\/([^"/{}*]+)"[\s\S]*?"FromFile"\s*:\s*"([^"]+\.png)"/gi;

  for (let match = pattern.exec(stripped); match; match = pattern.exec(stripped)) {
    const [, characterName, fromFile] = match;
    if (fromFile.includes("{{")) {
      continue;
    }

    const existing = loads.get(characterName);
    if (!existing || isBasePortraitFromFile(characterName, fromFile)) {
      loads.set(characterName, fromFile);
    }
  }

  return loads;
}

function registerContentPatcherPortrait(manifest, modId, modKey, characterName, source, copiedKeys, copied) {
  const key = `${modId}/${characterName}`;
  if (copiedKeys.has(key)) {
    return false;
  }

  if (!existsSync(source) || !source.toLowerCase().endsWith(".png")) {
    return false;
  }

  const output = join(cpOutRoot, modKey, `${safeSegment(characterName)}.png`);
  const url = copyPortrait(source, output);
  manifest.portraitSources[`${modId}/${characterName}`] = url;
  manifest.portraitSources[`${modKey}/${safeSegment(characterName)}`] = url;
  copiedKeys.add(key);
  copied.push(key);
  return true;
}

function syncContentPatcherPortraits(manifest, copiedKeys, copied) {
  for (const contentPath of collectJsonFiles(modsDir)) {
    let rawText;
    try {
      rawText = readFileSync(contentPath, "utf-8");
    } catch {
      continue;
    }

    const modDir = findContentPackRoot(contentPath);
    const modManifest = readJson(join(modDir, "manifest.json"));
    const modId = modManifest?.UniqueID ?? modManifest?.Name ?? relative(modsDir, modDir);
    const modKey = safeSegment(modId);
    const portraitLoads = new Map();

    const content = readJson(contentPath);
    if (Array.isArray(content?.Changes)) {
      for (const change of content.Changes) {
        const target = String(change.Target ?? "");
        const match = /^Portraits\/([^/{}]+)$/i.exec(target);
        if (!match || !change.FromFile) {
          continue;
        }

        const fromFile = String(change.FromFile);
        if (fromFile.includes("{{")) {
          continue;
        }

        portraitLoads.set(match[1], fromFile);
      }
    }

    for (const [characterName, fromFile] of extractPortraitLoadsFromText(rawText)) {
      const existing = portraitLoads.get(characterName);
      if (!existing || isBasePortraitFromFile(characterName, fromFile)) {
        portraitLoads.set(characterName, fromFile);
      }
    }

    for (const [characterName, fromFile] of portraitLoads) {
      registerContentPatcherPortrait(
        manifest,
        modId,
        modKey,
        characterName,
        resolve(modDir, fromFile),
        copiedKeys,
        copied,
      );
    }
  }

}

function findCharacterPortraitsRoot(modRoot) {
  const direct = join(modRoot, "assets", "CharacterFiles", "Portraits");
  if (existsSync(direct)) {
    return { portraitsRoot: direct, packRoot: modRoot };
  }

  if (!existsSync(modRoot)) {
    return null;
  }

  for (const entry of readdirSync(modRoot)) {
    const nestedRoot = join(modRoot, entry);
    if (!statSync(nestedRoot).isDirectory()) {
      continue;
    }

    const nested = join(nestedRoot, "assets", "CharacterFiles", "Portraits");
    if (existsSync(nested)) {
      return { portraitsRoot: nested, packRoot: nestedRoot };
    }
  }

  return null;
}

function syncCharacterPortraitAssetDirs(manifest, copiedKeys, copied) {
  if (!existsSync(modsDir)) {
    return 0;
  }

  let added = 0;
  for (const entry of readdirSync(modsDir)) {
    const modRoot = join(modsDir, entry);
    if (!statSync(modRoot).isDirectory()) {
      continue;
    }

    const located = findCharacterPortraitsRoot(modRoot);
    if (!located) {
      continue;
    }

    const { portraitsRoot, packRoot } = located;
    const modManifest = readJson(join(packRoot, "manifest.json"));
    const modId = modManifest?.UniqueID ?? modManifest?.Name ?? relative(modsDir, packRoot);
    const modKey = safeSegment(modId);

    for (const characterName of readdirSync(portraitsRoot)) {
      const characterDir = join(portraitsRoot, characterName);
      if (!statSync(characterDir).isDirectory()) {
        continue;
      }

      const source = join(characterDir, `${characterName}.png`);
      if (
        registerContentPatcherPortrait(
          manifest,
          modId,
          modKey,
          characterName,
          source,
          copiedKeys,
          copied,
        )
      ) {
        added += 1;
      }
    }
  }

  return added;
}

function indexOrphanPortraitFiles(manifest) {
  if (!existsSync(cpOutRoot)) {
    return 0;
  }

  let indexed = 0;
  for (const modKey of readdirSync(cpOutRoot)) {
    const modDir = join(cpOutRoot, modKey);
    if (!statSync(modDir).isDirectory()) {
      continue;
    }

    for (const fileName of readdirSync(modDir)) {
      if (!fileName.toLowerCase().endsWith(".png")) {
        continue;
      }

      const characterName = fileName.replace(/\.png$/i, "");
      const key = `${modKey}/${characterName}`;
      if (manifest.portraitSources[key]) {
        continue;
      }

      manifest.portraitSources[key] = toPublicUrl(webPublic, join(modDir, fileName));
      indexed += 1;
    }
  }

  return indexed;
}

const PORTRAIT_ASSET_ALIASES = {
  Leo: "ParrotBoy",
  LeoMainland: "ParrotBoy",
};

function indexVanillaPortraitFiles(manifest) {
  manifest.portraits = manifest.portraits ?? {};
  const vanillaOut = join(webPublic, "Portraits");
  if (!existsSync(vanillaOut)) {
    return 0;
  }

  let indexed = 0;
  for (const fileName of readdirSync(vanillaOut)) {
    if (!fileName.toLowerCase().endsWith(".png") || fileName.includes("/")) {
      continue;
    }

    const characterName = fileName.replace(/\.png$/i, "");
    const url = toPublicUrl(webPublic, join(vanillaOut, fileName));
    if (!manifest.portraits[characterName]) {
      manifest.portraits[characterName] = url;
      indexed += 1;
    }
    if (!manifest.portraitSources[characterName]) {
      manifest.portraitSources[characterName] = url;
    }
  }

  for (const [npcName, assetName] of Object.entries(PORTRAIT_ASSET_ALIASES)) {
    const url = manifest.portraits[assetName] ?? manifest.portraitSources[assetName];
    if (!url) {
      continue;
    }

    if (!manifest.portraits[npcName]) {
      manifest.portraits[npcName] = url;
    }
    if (!manifest.portraitSources[npcName]) {
      manifest.portraitSources[npcName] = url;
    }
  }

  return indexed;
}

function findContentPackRoot(filePath) {
  let current = dirname(filePath);
  while (current.startsWith(modsDir)) {
    if (existsSync(join(current, "manifest.json"))) {
      return current;
    }

    const parent = dirname(current);
    if (parent === current) break;
    current = parent;
  }

  return dirname(filePath);
}

function main() {
  const manifest = existsSync(localManifestPath)
    ? readJson(localManifestPath) ?? { version: 1 }
    : { version: 1 };

  manifest.version = manifest.version || 1;
  manifest.portraitSources = {};
  manifest.portraitGrids = manifest.portraitGrids ?? {};

  const portraitureCopied = syncPortraitureSources(manifest);
  const copiedKeys = new Set();
  const cpCopied = [];
  syncContentPatcherPortraits(manifest, copiedKeys, cpCopied);

  const assetDirCopied = syncCharacterPortraitAssetDirs(manifest, copiedKeys, cpCopied);
  const orphanIndexed = indexOrphanPortraitFiles(manifest);
  const vanillaIndexed = indexVanillaPortraitFiles(manifest);

  writeFileSync(localManifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  console.log(`Wrote ${localManifestPath}`);
  console.log(`Portraiture sources: ${portraitureCopied.length}`);
  console.log(`Content Patcher portrait sources: ${cpCopied.length}`);
  console.log(`CharacterFiles portrait dirs: ${assetDirCopied}`);
  console.log(`Indexed orphan portrait files: ${orphanIndexed}`);
  console.log(`Indexed vanilla portrait files: ${vanillaIndexed}`);
}

main();
