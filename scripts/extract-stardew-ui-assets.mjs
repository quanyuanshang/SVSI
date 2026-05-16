#!/usr/bin/env node
import { existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { basename, dirname, extname, join, relative, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const TARGETS = [
  "Content/LooseSprites/Cursors.xnb",
  "Content/LooseSprites/Cursors2.xnb",
  "Content/LooseSprites/Cursors_1_6.xnb",
  "Content/LooseSprites/textBox.xnb",
  "Content/LooseSprites/chatBox.xnb",
  "Content/LooseSprites/Billboard.xnb",
  "Content/LooseSprites/JunimoNote.xnb",
  "Content/LooseSprites/SpecialOrdersBoard.xnb",
  "Content/Maps/MenuTiles.xnb",
  "Content/Maps/MenuTilesUncolored.xnb",
];

const ATLAS_KEYS = {
  "LooseSprites/Cursors": "cursors",
  "LooseSprites/Cursors2": "cursors2",
  "LooseSprites/Cursors_1_6": "cursors_1_6",
  "LooseSprites/textBox": "textBox",
  "LooseSprites/chatBox": "chatBox",
  "LooseSprites/Billboard": "billboard",
  "LooseSprites/JunimoNote": "junimoNote",
  "LooseSprites/SpecialOrdersBoard": "specialOrdersBoard",
  "Maps/MenuTiles": "menuTiles",
  "Maps/MenuTilesUncolored": "menuTilesUncolored",
};

const args = parseArgs(process.argv.slice(2));
const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDir, "..");
const gameDir = args["game-dir"] ? resolve(args["game-dir"]) : null;
const outputDir = resolve(args["out"] ?? join(projectRoot, "web/public/generated/stardew-ui"));
const explicitTool = args["tool"] ? resolve(args["tool"]) : null;

if (!gameDir) {
  fail('Missing --game-dir "C:/Path/To/Stardew Valley"');
}

if (!existsSync(gameDir)) {
  fail(`Game directory does not exist: ${gameDir}`);
}

const tool = explicitTool ?? findXnbTool();
mkdirSync(outputDir, { recursive: true });

const manifest = {
  version: 1,
  sourceGameDir: gameDir,
  generatedAt: new Date().toISOString(),
  atlases: {},
  sprites: createPlaceholderSprites(),
  portraits: {},
  missingSources: [],
  extractionNotes: [],
};

if (!tool) {
  manifest.extractionNotes.push(
    "No xnbcli or StardewXnbHack executable was found. Install one, add it to PATH, or pass --tool.",
  );
}

for (const target of TARGETS) {
  const source = join(gameDir, ...target.split("/"));
  if (!existsSync(source)) {
    manifest.missingSources.push(target);
    continue;
  }

  const relativeNoExt = target.replace(/^Content\//, "").replace(/\.xnb$/i, "");
  const pngOutput = join(outputDir, `${relativeNoExt}.png`);
  mkdirSync(dirname(pngOutput), { recursive: true });

  if (tool) {
    tryUnpack(tool, source, dirname(pngOutput), pngOutput, manifest.extractionNotes);
  }

  if (existsSync(pngOutput)) {
    manifest.atlases[ATLAS_KEYS[relativeNoExt] ?? toCamelKey(relativeNoExt)] =
      toPublicUrl(outputDir, pngOutput);
  }
}

extractPortraitDirectory({
  gameDir,
  outputDir,
  tool,
  manifest,
  sourceSubdir: "Content/Portraits",
  manifestPrefix: "",
});

extractPortraitDirectory({
  gameDir,
  outputDir,
  tool,
  manifest,
  sourceSubdir: "Content/Characters",
  manifestPrefix: "characters/",
});

writeFileSync(join(outputDir, "manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
writeFileSync(join(outputDir, "atlas-preview.html"), buildPreviewHtml(manifest));

console.log(`Generated manifest: ${join(outputDir, "manifest.json")}`);
console.log(`Generated atlas preview: ${join(outputDir, "atlas-preview.html")}`);
if (!tool) {
  console.log("No XNB unpack tool found. See docs/STARDew_UI_ASSETS.md for setup.");
}

function parseArgs(argv) {
  const parsed = {};
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (!arg.startsWith("--")) {
      continue;
    }

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

function findXnbTool() {
  const envTool = process.env.STARDEW_XNB_TOOL;
  if (envTool && existsSync(envTool)) {
    return resolve(envTool);
  }

  for (const name of ["xnbcli", "xnbcli.exe", "StardewXnbHack.exe", "StardewXnbHack"]) {
    const result = spawnSync(process.platform === "win32" ? "where" : "which", [name], {
      encoding: "utf8",
    });
    const match = result.stdout?.split(/\r?\n/).find(Boolean);
    if (match) {
      return match.trim();
    }
  }

  return null;
}

function tryUnpack(tool, source, outDir, expectedPng, notes) {
  if (existsSync(expectedPng)) {
    return;
  }

  const commands = [
    [tool, ["unpack", source, outDir]],
    [tool, [source, outDir]],
    [tool, ["extract", source, outDir]],
  ];

  for (const [command, commandArgs] of commands) {
    const result = spawnSync(command, commandArgs, { encoding: "utf8" });
    if (result.status === 0 && existsSync(expectedPng)) {
      return;
    }
  }

  notes.push(`Could not unpack ${source}. Try extracting manually to ${expectedPng}.`);
}

function extractPortraitDirectory({
  gameDir,
  outputDir,
  tool,
  manifest,
  sourceSubdir,
  manifestPrefix,
}) {
  const sourceDir = join(gameDir, ...sourceSubdir.split("/"));
  if (!existsSync(sourceDir)) {
    manifest.missingSources.push(sourceSubdir);
    return;
  }

  for (const entry of readdirSync(sourceDir)) {
    if (extname(entry).toLowerCase() !== ".xnb") {
      continue;
    }

    const source = join(sourceDir, entry);
    if (!statSync(source).isFile()) {
      continue;
    }

    const characterName = basename(entry, ".xnb");
    const output = join(outputDir, "Portraits", manifestPrefix, `${characterName}.png`);
    mkdirSync(dirname(output), { recursive: true });

    if (tool) {
      tryUnpack(tool, source, dirname(output), output, manifest.extractionNotes);
    }

    if (existsSync(output)) {
      manifest.portraits[`${manifestPrefix}${characterName}`] = toPublicUrl(outputDir, output);
      if (!manifestPrefix) {
        manifest.portraits[characterName.toLowerCase()] = toPublicUrl(outputDir, output);
      }
    }
  }
}

function createPlaceholderSprites() {
  return {
    "ui.panel.default": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 64, h: 64 },
      nineSlice: { top: 12, right: 12, bottom: 12, left: 12 },
      note: "Placeholder rect. Use atlas-preview.html to manually mark exact coordinates.",
    },
    "ui.panel.textbox": {
      atlas: "textBox",
      rect: { x: 0, y: 0, w: 64, h: 64 },
      nineSlice: { top: 12, right: 12, bottom: 12, left: 12 },
      note: "Placeholder rect.",
    },
    "ui.button.default": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 48, h: 48 },
      nineSlice: { top: 10, right: 10, bottom: 10, left: 10 },
      note: "Placeholder rect. Update after inspecting atlas-preview.html.",
    },
    "icon.lock": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 16, h: 16 },
      note: "Placeholder rect.",
    },
    "icon.heart": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 16, h: 16 },
      note: "Placeholder rect.",
    },
    "icon.star": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 16, h: 16 },
      note: "Placeholder rect.",
    },
    "icon.arrow": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 16, h: 16 },
      note: "Placeholder rect.",
    },
    "icon.check": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 16, h: 16 },
      note: "Placeholder rect.",
    },
    "icon.warning": {
      atlas: "cursors",
      rect: { x: 0, y: 0, w: 16, h: 16 },
      note: "Placeholder rect.",
    },
  };
}

function buildPreviewHtml(manifest) {
  const atlasCards = Object.entries(manifest.atlases)
    .map(
      ([key, url]) => `
        <section class="atlas">
          <h2>${escapeHtml(key)}</h2>
          <p>${escapeHtml(url)}</p>
          <img src="${escapeHtml(url)}" alt="${escapeHtml(key)} atlas" />
        </section>
      `,
    )
    .join("\n");

  return `<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>Stardew UI Atlas Preview</title>
  <style>
    body { font-family: sans-serif; background: #2f261c; color: #fff4d0; padding: 24px; }
    .atlas { margin-bottom: 32px; padding: 16px; border: 1px solid #9b7442; background: #49351f; }
    img { max-width: 100%; image-rendering: pixelated; background: #111; }
    p { color: #d8c49a; }
  </style>
</head>
<body>
  <h1>Stardew UI Atlas Preview</h1>
  <p>Use this page to inspect extracted atlases and manually update rects in manifest.json.</p>
  ${atlasCards || "<p>No atlases extracted yet.</p>"}
</body>
</html>
`;
}

function toPublicUrl(outputDir, file) {
  return `/generated/stardew-ui/${relative(outputDir, file).replace(/\\/g, "/")}`;
}

function toCamelKey(value) {
  return value
    .replace(/\\/g, "/")
    .split("/")
    .map((part, index) => {
      const clean = part.replace(/[^A-Za-z0-9]+/g, " ");
      const words = clean.split(/\s+/).filter(Boolean);
      return words
        .map((word, wordIndex) =>
          index === 0 && wordIndex === 0
            ? word[0].toLowerCase() + word.slice(1)
            : word[0].toUpperCase() + word.slice(1),
        )
        .join("");
    })
    .join("");
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function fail(message) {
  console.error(message);
  process.exit(1);
}
