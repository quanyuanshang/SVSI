# Portrait Overrides

The preferred portrait source is the local generated Stardew asset manifest in
`public/generated/stardew-ui/manifest.json`. Generate it with:

```powershell
# If your shell is already in D:\cs\dev\StardewStoryInspector:
npm --prefix web run extract:stardew-ui -- `
  --game-dir "C:\Path\To\Stardew Valley"

# If your shell is in D:\cs\dev:
npm --prefix StardewStoryInspector/web run extract:stardew-ui -- `
  --game-dir "C:\Path\To\Stardew Valley"
```

See `docs/STARDew_UI_ASSETS.md` for the full local extraction workflow.

This folder remains a manual override fallback. Put manually prepared character
portraits here when you do not want to edit the generated manifest. The
frontend checks generated portraits first, then these paths, then initials:

- `public/portraits/Sebastian.png`
- `public/portraits/sebastian.png`
- `public/portraits/<sourceModId>/Sebastian.png`
- `public/portraits/<sourceModId>/sebastian.png`

`webp` also works. For mod folders, non-file-safe characters in `sourceModId`
are normalized to underscores.

Recommended size:

- Square PNG or WebP.
- 64x64, 96x96, or 128x128.
- Pixel-art portraits should keep crisp edges; CSS uses `image-rendering:
  pixelated`.

How to find images:

- Vanilla portraits are in Stardew Valley content assets under the game's
  `Content/Portraits` data, usually packed in `.xnb`.
- Mod portraits are usually inside each mod under paths like `assets`,
  `Portraits`, `Characters`, or Content Patcher `FromFile` targets referenced
  by `content.json`.
- If you extract or crop images manually, name the file after the raw NPC id
  used by the event data, e.g. `Sebastian.png`, `Abigail.png`, or the mod NPC
  internal id.

Do not commit copyrighted game assets unless you are sure this repo is allowed
to contain them. Local-only files in this folder work during development.
