#!/usr/bin/env node
/**
 * Print + preview sprite rects used on event node cards and related UI.
 *
 * Usage — pick ONE based on your current directory:
 *
 * If cwd is D:\cs\dev:
 *   node StardewStoryInspector/scripts/preview-event-node-sprites.mjs
 *   npm --prefix StardewStoryInspector/web run preview:event-node-sprites
 *
 * If cwd is D:\cs\dev\StardewStoryInspector (already inside project):
 *   node scripts/preview-event-node-sprites.mjs
 *   npm --prefix web run preview:event-node-sprites
 *
 * Outputs:
 *   - Console table (coordinates + where each key is used in the app)
 *   - web/public/generated/stardew-ui/sprite-position-preview.html
 */

import { existsSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDir, "..");
const webRoot = join(projectRoot, "web");
const publicRoot = join(webRoot, "public", "generated", "stardew-ui");
const seedManifestPath = join(webRoot, "src", "stardew-ui", "stardew-ui-manifest.seed.json");
const outputHtmlPath = join(publicRoot, "sprite-position-preview.html");

/** Sprites currently wired into EventNodeCard / ActionBoard / StorylineOverview */
const USAGE_GROUPS = [
  {
    title: "事件卡片右上角 (EventNodeCard corner)",
    entries: [
      { usage: "ready / Current", spriteKey: "ui.shop.itemIconBackground" },
      { usage: "later / AvailableLater", spriteKey: "icon.scrollDown" },
      { usage: "locked / Locked", spriteKey: "ui.scrollBar.back" },
      { usage: "next 列", spriteKey: "ui.scrollBar.front" },
      { usage: "recent / Triggered", spriteKey: "ui.shop.itemRowBackground" },
      { usage: "fallback", spriteKey: "ui.windowBorder.default" },
    ],
  },
  {
    title: "事件卡片 hint 行 (EventNodeCard hint)",
    entries: [
      { usage: "blocked", spriteKey: "ui.scrollBar.back" },
      { usage: "default hint", spriteKey: "icon.scrollDown" },
    ],
  },
  {
    title: "行动板 / 故事线 legend (ActionBoard & StorylineOverview)",
    entries: [
      { usage: "ready legend", spriteKey: "ui.shop.itemIconBackground" },
      { usage: "later legend", spriteKey: "icon.scrollDown" },
      { usage: "locked legend", spriteKey: "ui.scrollBar.back" },
      { usage: "recent legend", spriteKey: "ui.shop.itemRowBackground" },
      { usage: "next legend", spriteKey: "ui.scrollBar.front" },
    ],
  },
];

const COLORS = [
  "#2f6d46",
  "#d89b2b",
  "#ad3f32",
  "#467fb8",
  "#7a4d9c",
  "#5f3b20",
  "#1f7a49",
  "#b45309",
];

function main() {
  if (!existsSync(seedManifestPath)) {
    fail(`Seed manifest not found: ${seedManifestPath}`);
  }

  const manifest = JSON.parse(readFileSync(seedManifestPath, "utf8"));
  const rows = collectRows(manifest);

  printConsoleTable(rows);
  mkdirSync(publicRoot, { recursive: true });
  writeFileSync(outputHtmlPath, buildHtml(manifest, rows), "utf8");

  console.log("");
  console.log(`HTML preview: ${outputHtmlPath}`);
  console.log("Dev server URL:  http://localhost:5173/generated/stardew-ui/sprite-position-preview.html");
  console.log("(Start with: npm --prefix StardewStoryInspector/web run dev)");
}

function collectRows(manifest) {
  const seen = new Set();
  const rows = [];

  for (const group of USAGE_GROUPS) {
    for (const entry of group.entries) {
      const key = `${group.title}::${entry.usage}::${entry.spriteKey}`;
      if (seen.has(key)) {
        continue;
      }
      seen.add(key);

      const sprite = manifest.sprites?.[entry.spriteKey];
      if (!sprite) {
        rows.push({
          group: group.title,
          usage: entry.usage,
          spriteKey: entry.spriteKey,
          asset: "(missing in seed manifest)",
          rect: null,
          url: null,
          color: COLORS[rows.length % COLORS.length],
        });
        continue;
      }

      const assetDef = manifest.assets?.[sprite.asset];
      const url = assetDef?.url ?? null;
      const diskPath = url ? join(publicRoot, url.replace(/^\/generated\/stardew-ui\//, "")) : null;

      rows.push({
        group: group.title,
        usage: entry.usage,
        spriteKey: entry.spriteKey,
        asset: sprite.asset,
        rect: sprite.rect,
        notes: sprite.notes ?? "",
        url,
        diskPath,
        exists: diskPath ? existsSync(diskPath) : false,
        color: COLORS[rows.length % COLORS.length],
      });
    }
  }

  return rows;
}

function printConsoleTable(rows) {
  console.log("");
  console.log("=== Event node / UI sprite positions (from seed manifest) ===");
  console.log("");

  for (const row of rows) {
    console.log(`[${row.group}] ${row.usage}`);
    console.log(`  spriteKey: ${row.spriteKey}`);
    if (!row.rect) {
      console.log("  rect:      (not found)");
      console.log("");
      continue;
    }

    const { x, y, w, h } = row.rect;
    console.log(`  asset:     ${row.asset}`);
    console.log(`  rect:      x=${x} y=${y} w=${w} h=${h}`);
    console.log(`  atlas:     ${row.url ?? "(no url)"}`);
    if (row.diskPath) {
      console.log(`  file:      ${row.diskPath}`);
      console.log(`  exists:    ${row.exists ? "yes" : "NO — extract assets first"}`);
    }
    if (row.notes) {
      console.log(`  notes:     ${row.notes}`);
    }
    console.log(`  在图上找:  打开 Cursors.png，从左上角数到 (${x}, ${y})，区域大小 ${w}×${h} px`);
    console.log("");
  }

  console.log("--- All unique spriteKeys in this report ---");
  const uniqueKeys = [...new Set(rows.map((r) => r.spriteKey))];
  for (const key of uniqueKeys) {
    console.log(`  - ${key}`);
  }
}

function buildHtml(manifest, rows) {
  const byAsset = groupByAsset(rows);

  const atlasSections = Object.entries(byAsset)
    .map(([asset, assetRows]) => buildAtlasSection(asset, assetRows, manifest))
    .join("\n");

  const cardSections = rows
    .map(
      (row) => `
      <article class="sprite-card" style="--accent:${row.color}">
        <header>
          <h3>${escapeHtml(row.spriteKey)}</h3>
          <p>${escapeHtml(row.group)} · ${escapeHtml(row.usage)}</p>
        </header>
        ${row.rect ? buildPreviewCell(row) : "<p class='warn'>Missing in seed manifest</p>"}
        ${row.rect ? `<dl>${buildMetaDl(row)}</dl>` : ""}
      </article>`,
    )
    .join("\n");

  return `<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <title>Event Node Sprite Position Preview</title>
  <style>
    :root {
      font-family: Consolas, "Segoe UI", sans-serif;
      color: #3e2b1f;
      background: #f7ecd2;
    }
    body { margin: 0; padding: 20px; }
    h1, h2 { margin: 0 0 8px; }
    .intro { margin-bottom: 20px; max-width: 900px; line-height: 1.5; }
    .atlas-block {
      margin: 24px 0;
      padding: 16px;
      background: #fff8df;
      border: 2px solid rgba(95,59,32,.2);
      border-radius: 12px;
      overflow: auto;
    }
    .atlas-wrap {
      position: relative;
      display: inline-block;
      line-height: 0;
    }
    .atlas-wrap img {
      display: block;
      image-rendering: pixelated;
      max-width: none;
    }
    .atlas-wrap canvas {
      position: absolute;
      left: 0;
      top: 0;
      pointer-events: none;
    }
    .sprite-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 14px;
      margin-top: 24px;
    }
    .sprite-card {
      padding: 12px;
      border-radius: 12px;
      border: 2px solid var(--accent);
      background: rgba(255,248,226,.9);
    }
    .sprite-card h3 { margin: 0; font-size: 0.95rem; word-break: break-all; }
    .sprite-card header p { margin: 4px 0 10px; font-size: 0.82rem; color: #735d45; }
    .preview {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
      margin-bottom: 10px;
    }
    .preview-box {
      display: grid;
      gap: 4px;
      justify-items: center;
    }
    .preview-box span { font-size: 0.75rem; color: #735d45; }
    .preview-native, .preview-scaled {
      border: 1px solid rgba(95,59,32,.25);
      background:
        linear-gradient(45deg, rgba(95,59,32,.08) 25%, transparent 25%),
        linear-gradient(-45deg, rgba(95,59,32,.08) 25%, transparent 25%),
        #fff;
      background-size: 12px 12px;
      image-rendering: pixelated;
    }
    .preview-scaled { width: 64px; height: 64px; }
    dl { margin: 0; font-size: 0.8rem; display: grid; grid-template-columns: auto 1fr; gap: 2px 8px; }
    dt { color: #735d45; }
    dd { margin: 0; word-break: break-all; }
    .warn { color: #ad3f32; font-weight: bold; }
    code { background: rgba(95,59,32,.1); padding: 1px 4px; border-radius: 4px; }
  </style>
</head>
<body>
  <h1>Event Node Sprite Position Preview</h1>
  <p class="intro">
    红框 = seed manifest 中的 rect。左侧为 atlas 原图位置，右侧卡片为裁切预览（1× 与放大到 64px）。
    坐标原点在 atlas 左上角，单位像素。与 <code>/stardew-assets-debug</code> 使用同一套 manifest。
  </p>
  ${atlasSections}
  <h2>逐图标预览</h2>
  <div class="sprite-grid">${cardSections}</div>
  <script>
    for (const block of document.querySelectorAll('[data-atlas-url]')) {
      const img = block.querySelector('img');
      const canvas = block.querySelector('canvas');
      const rects = JSON.parse(block.dataset.rects || '[]');
      img.addEventListener('load', () => {
        canvas.width = img.naturalWidth;
        canvas.height = img.naturalHeight;
        canvas.style.width = img.width + 'px';
        canvas.style.height = img.height + 'px';
        const ctx = canvas.getContext('2d');
        for (const r of rects) {
          ctx.strokeStyle = r.color;
          ctx.lineWidth = 2;
          ctx.strokeRect(r.x, r.y, r.w, r.h);
          ctx.fillStyle = r.color;
          ctx.globalAlpha = 0.15;
          ctx.fillRect(r.x, r.y, r.w, r.h);
          ctx.globalAlpha = 1;
          ctx.font = '12px sans-serif';
          ctx.fillStyle = '#000';
          ctx.fillText(r.label, r.x + 2, r.y + 12);
        }
      });
    }
  </script>
</body>
</html>`;
}

function buildAtlasSection(asset, assetRows, manifest) {
  const assetDef = manifest.assets?.[asset];
  const url = assetDef?.url ?? "";
  const rects = assetRows
    .filter((r) => r.rect)
    .map((r) => ({
      x: r.rect.x,
      y: r.rect.y,
      w: r.rect.w,
      h: r.rect.h,
      color: r.color,
      label: r.spriteKey.split(".").pop(),
    }));

  const legend = assetRows
    .map(
      (r) =>
        `<li><span style="color:${r.color}">■</span> <code>${escapeHtml(r.spriteKey)}</code> — ${escapeHtml(r.usage)} (${r.rect ? `x=${r.rect.x} y=${r.rect.y} ${r.rect.w}×${r.rect.h}` : "missing"})</li>`,
    )
    .join("\n");

  return `
  <section class="atlas-block">
    <h2>${escapeHtml(asset)}</h2>
    <ul>${legend}</ul>
    <div class="atlas-wrap" data-atlas-url="${escapeHtml(url)}" data-rects='${JSON.stringify(rects)}'>
      <img src="${escapeHtml(url)}" alt="${escapeHtml(asset)}" />
      <canvas></canvas>
    </div>
  </section>`;
}

function buildPreviewCell(row) {
  const { x, y, w, h } = row.rect;
  const scale = Math.max(1, Math.floor(64 / Math.max(w, h)));
  const bgW = "auto";
  const bgH = "auto";
  const nativeStyle = `width:${w}px;height:${h}px;background-image:url('${row.url}');background-position:-${x}px -${y}px;background-size:${bgW};background-repeat:no-repeat;image-rendering:pixelated;`;
  const scaledStyle = `background-image:url('${row.url}');background-position:-${x * scale}px -${y * scale}px;background-size:auto;background-repeat:no-repeat;image-rendering:pixelated;`;

  return `
    <div class="preview">
      <div class="preview-box">
        <span>原尺寸 ${w}×${h}</span>
        <div class="preview-native" style="${nativeStyle}"></div>
      </div>
      <div class="preview-box">
        <span>放大 ~${scale}× (≈事件卡角标)</span>
        <div class="preview-scaled" style="${scaledStyle}"></div>
      </div>
    </div>`;
}

function buildMetaDl(row) {
  const { x, y, w, h } = row.rect;
  return `
    <dt>usage</dt><dd>${escapeHtml(row.usage)}</dd>
    <dt>asset</dt><dd>${escapeHtml(row.asset)}</dd>
    <dt>rect</dt><dd>x=${x} y=${y} w=${w} h=${h}</dd>
    <dt>url</dt><dd>${escapeHtml(row.url ?? "")}</dd>`;
}

function groupByAsset(rows) {
  const map = {};
  for (const row of rows) {
    const asset = row.asset ?? "unknown";
    if (!map[asset]) {
      map[asset] = [];
    }
    map[asset].push(row);
  }
  return map;
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

main();
