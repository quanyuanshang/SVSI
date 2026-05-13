# Stardew Story Inspector Web

The Vite dev server exposes local runtime JSON from `STORY_INSPECTOR_DATA_DIR`.

- `GET /api/story-state` reads `runtime/state/story-state.evaluated.json`.
- `GET /api/event-history` reads `runtime/history/event-history.json`.

The app has two views:

- Today: current day timeline and conflict panel.
- Progress: recorded event history grouped by year, season, and day.
# StardewStoryInspector Web

This Vite app can read the live evaluated story report from the local Stardew Valley export directory through the Vite dev server API.

Available dev endpoints:

- `GET /api/health`
- `GET /api/story-state`

`/api/story-state` reads:

`<STORY_INSPECTOR_DATA_DIR>/runtime/state/story-state.evaluated.json`

## Start

Windows PowerShell:

```powershell
$env:STORY_INSPECTOR_DATA_DIR="D:\SteamLibrary\steamapps\common\Stardew Valley\StardewStoryInspector"
npm run dev
```

If `STORY_INSPECTOR_DATA_DIR` is missing, `/api/story-state` returns:

```json
{ "error": "STORY_INSPECTOR_DATA_DIR is not set" }
```

If the evaluated report file does not exist, `/api/story-state` returns:

```json
{
  "error": "story-state.evaluated.json not found",
  "path": "..."
}
```
