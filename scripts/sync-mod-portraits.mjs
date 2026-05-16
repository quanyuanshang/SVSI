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

function syncContentPatcherPortraits(manifest) {
  const copied = [];
  for (const contentPath of collectJsonFiles(modsDir)) {
    const content = readJson(contentPath);
    const changes = Array.isArray(content?.Changes) ? content.Changes : [];
    if (changes.length === 0) continue;

    const modDir = findContentPackRoot(contentPath);
    const modManifest = readJson(join(modDir, "manifest.json"));
    const modId = modManifest?.UniqueID ?? modManifest?.Name ?? relative(modsDir, modDir);
    const modKey = safeSegment(modId);

    for (const change of changes) {
      const target = String(change.Target ?? "");
      const match = /^Portraits\/([^/{}]+)$/i.exec(target);
      if (!match || !change.FromFile) continue;

      const characterName = match[1];
      const source = resolve(modDir, String(change.FromFile));
      if (!existsSync(source) || !source.toLowerCase().endsWith(".png")) continue;

      const output = join(cpOutRoot, modKey, `${safeSegment(characterName)}.png`);
      const url = copyPortrait(source, output);
      manifest.portraitSources[`${modId}/${characterName}`] = url;
      manifest.portraitSources[`${modKey}/${safeSegment(characterName)}`] = url;
      copied.push(`${modId}/${characterName}`);
    }
  }

  return copied;
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
  const cpCopied = syncContentPatcherPortraits(manifest);

  writeFileSync(localManifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  console.log(`Wrote ${localManifestPath}`);
  console.log(`Portraiture sources: ${portraitureCopied.length}`);
  console.log(`Content Patcher portrait sources: ${cpCopied.length}`);
}

main();
