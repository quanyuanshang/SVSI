import { useEffect, useMemo, useRef, useState } from "react";
import {
  buildPortraitFrameStyle,
  computePortraitFrameRect,
  deriveBaseGrid,
  getMaxExpressionIndex,
  resolvePortraitSource,
  type PortraitGridOverride,
  type StardewManifest,
  type StardewRect,
} from "../lib/stardewAssets";
import { usePortraitGrid } from "../stardew-ui/usePortraitGrid";
import { CharacterPortrait } from "./CharacterPortrait";
import { PortraitFrameView } from "./PortraitFrameView";
import { SpriteCropCanvas } from "./SpriteCropCanvas";

export function PortraitDebugSection({ manifest }: { manifest: StardewManifest | null }) {
  const portraits = manifest?.portraits ?? {};
  const portraitSources = manifest?.portraitSources ?? {};
  const portraitEntries = useMemo(
    () => {
      const names = new Set(
        Object.keys(portraits)
        .filter((key) => !key.includes("/") && key[0] === key[0]?.toUpperCase())
          .map((key) => key),
      );

      for (const key of Object.keys(portraitSources)) {
        const name = key.slice(key.lastIndexOf("/") + 1);
        if (name && name[0] === name[0]?.toUpperCase()) {
          names.add(name);
        }
      }

      return [...names].sort();
    },
    [portraitSources, portraits],
  );
  const [selectedName, setSelectedName] = useState("Wizard");
  const [expressionIndex, setExpressionIndex] = useState(0);
  const [manualOverride, setManualOverride] = useState<PortraitGridOverride>({});
  const imageRef = useRef<HTMLImageElement | null>(null);
  const [, refreshOverlay] = useState(0);

  useEffect(() => {
    if (portraitEntries.length > 0 && !portraitEntries.includes(selectedName)) {
      setSelectedName(portraitEntries[0]);
    }
  }, [portraitEntries, selectedName]);

  const resolved = useMemo(
    () => resolvePortraitSource(selectedName, manifest),
    [manifest, selectedName],
  );
  const effectiveOverride = useMemo(
    () => ({ ...(resolved?.gridOverride ?? {}), ...manualOverride }),
    [manualOverride, resolved?.gridOverride],
  );
  const hasOverride = Object.values(effectiveOverride).some((value) => value != null);
  const { grid, hdSize, baseSize } = usePortraitGrid(
    resolved?.hdUrl,
    resolved?.baseUrl,
    hasOverride ? effectiveOverride : null,
  );
  const frame = useMemo(
    () => (grid ? computePortraitFrameRect(expressionIndex, grid) : null),
    [expressionIndex, grid],
  );
  const maxExpressionIndex = grid ? getMaxExpressionIndex(grid) : 0;
  const cssPreviewStyle =
    resolved?.hdUrl && frame ? buildPortraitFrameStyle(resolved.hdUrl, frame, 96, hdSize) : null;
  const baseGrid = baseSize ? deriveBaseGrid(baseSize) : null;

  if (portraitEntries.length === 0) {
    return (
      <section className="panel stardew-assets-debug__portraits">
        <h2>Portrait debug</h2>
        <p className="empty-state">No portraits found in the generated manifest.</p>
      </section>
    );
  }

  return (
    <section className="panel stardew-assets-debug__portraits">
      <div className="stardew-assets-debug__portraits-intro">
        <h2>Portrait debug</h2>
        <p>
          Base sheet grid comes from vanilla 64px portrait cells. HD frame size is derived from
          the selected sheet dimensions and the same face layout.
        </p>
        <p className="debug-note">
          Loaded {portraitEntries.length} character names, {Object.keys(portraitSources).length} portrait sources.
        </p>
      </div>

      <div className="stardew-assets-debug__portraits-grid">
        <aside className="stardew-assets-debug__portraits-sidebar">
          <h3>Characters</h3>
          <div className="debug-portrait-list">
            {portraitEntries.map((name) => (
              <button
                className={name === selectedName ? "is-selected" : ""}
                key={name}
                onClick={() => {
                  setSelectedName(name);
                  setExpressionIndex(0);
                  setManualOverride({});
                }}
                type="button"
              >
                <strong>{name}</strong>
                <span>{portraits[name] ?? findPortraitSourcePreview(name, portraitSources) ?? "(source pending)"}</span>
              </button>
            ))}
          </div>

          <fieldset className="debug-expression-controls">
            <legend>expressionIndex</legend>
            <div className="debug-expression-row">
              <button
                disabled={expressionIndex <= 0}
                onClick={() => setExpressionIndex((value) => Math.max(0, value - 1))}
                type="button"
              >
                -
              </button>
              <input
                max={maxExpressionIndex}
                min={0}
                onChange={(event) =>
                  setExpressionIndex(clamp(Number(event.target.value) || 0, 0, maxExpressionIndex))
                }
                type="number"
                value={expressionIndex}
              />
              <button
                disabled={expressionIndex >= maxExpressionIndex}
                onClick={() =>
                  setExpressionIndex((value) => Math.min(maxExpressionIndex, value + 1))
                }
                type="button"
              >
                +
              </button>
            </div>
          </fieldset>

          <fieldset className="debug-grid-override">
            <legend>Manual grid override</legend>
            {(["baseColumns", "baseRows", "frameWidth", "frameHeight"] as const).map((field) => (
              <label key={field}>
                {field}
                <input
                  onChange={(event) =>
                    setManualOverride((value) => ({
                      ...value,
                      [field]: parseOptionalInt(event.target.value),
                    }))
                  }
                  placeholder={grid ? String(grid[field]) : ""}
                  type="number"
                  value={manualOverride[field] ?? ""}
                />
              </label>
            ))}
            <pre className="debug-json-snippet">
              {JSON.stringify(
                Object.fromEntries(
                  Object.entries(effectiveOverride).filter(([, value]) => value != null),
                ),
                null,
                2,
              )}
            </pre>
          </fieldset>
        </aside>

        <section className="stardew-assets-debug__portraits-atlas">
          <h3>{selectedName}</h3>
          <dl className="debug-portrait-metrics">
            <div>
              <dt>Base sheet</dt>
              <dd>
                <code>{resolved?.baseUrl ?? "(none)"}</code>
                {baseSize ? ` - ${baseSize.width}x${baseSize.height}` : ""}
                {baseGrid ? ` - ${baseGrid.baseColumns} cols, ${baseGrid.baseRows} rows` : ""}
              </dd>
            </div>
            <div>
              <dt>HD sheet</dt>
              <dd>
                <code>{resolved?.hdUrl ?? "(none)"}</code>
                {hdSize ? ` - ${hdSize.width}x${hdSize.height}` : ""}
                {resolved ? ` - ${resolved.sourceLabel}` : ""}
              </dd>
            </div>
            {grid ? (
              <div>
                <dt>Derived grid</dt>
                <dd>
                  {grid.baseColumns} cols, {grid.baseRows} rows, frame {grid.frameWidth}x
                  {grid.frameHeight}, source {grid.source}
                  {grid.warning ? ` - ${grid.warning}` : ""}
                </dd>
              </div>
            ) : null}
            {frame ? (
              <div>
                <dt>Current rect</dt>
                <dd>
                  x={frame.x} y={frame.y} w={frame.w} h={frame.h}
                </dd>
              </div>
            ) : null}
          </dl>

          {resolved?.hdUrl && frame ? (
            <div className="debug-atlas-frame checkerboard">
              <img
                alt={`${selectedName} portrait sheet`}
                className="debug-portrait-sheet"
                draggable={false}
                onLoad={() => refreshOverlay((value) => value + 1)}
                ref={imageRef}
                src={resolved.hdUrl}
              />
              <AtlasRectOverlay
                image={imageRef.current}
                label={`expression ${frame.expressionIndex}`}
                rect={{ x: frame.x, y: frame.y, w: frame.w, h: frame.h }}
              />
            </div>
          ) : null}
        </section>

        <aside className="stardew-assets-debug__portraits-preview">
          <h3>Crop preview</h3>
          <div className="debug-preview-grid">
            {resolved?.hdUrl && frame ? (
              <SpriteCropCanvas atlasUrl={resolved.hdUrl} rect={frame} scale={4} />
            ) : null}
            {cssPreviewStyle ? (
              <span className="portrait-frame checkerboard" style={cssPreviewStyle} />
            ) : null}
            <div className="debug-portrait-component-row">
              <CharacterPortrait expressionIndex={expressionIndex} name={selectedName} size="sm" />
              <CharacterPortrait expressionIndex={expressionIndex} name={selectedName} size="md" />
              <CharacterPortrait expressionIndex={expressionIndex} name={selectedName} size="lg" />
            </div>
            {resolved ? (
              <PortraitFrameView
                baseSheetUrl={resolved.baseUrl}
                displaySize={96}
                expressionIndex={expressionIndex}
                gridOverride={hasOverride ? effectiveOverride : null}
                hdSheetUrl={resolved.hdUrl}
              />
            ) : null}
          </div>
        </aside>
      </div>
    </section>
  );
}

function AtlasRectOverlay({
  image,
  rect,
  label,
}: {
  image: HTMLImageElement | null;
  rect: StardewRect;
  label: string;
}) {
  if (!image || !image.naturalWidth || !image.naturalHeight) {
    return null;
  }

  const scaleX = image.clientWidth / image.naturalWidth;
  const scaleY = image.clientHeight / image.naturalHeight;

  return (
    <>
      <div
        className="debug-selection"
        style={{
          left: rect.x * scaleX,
          top: rect.y * scaleY,
          width: Math.max(1, rect.w * scaleX),
          height: Math.max(1, rect.h * scaleY),
        }}
      />
      <div
        className="debug-selection-label"
        style={{
          left: rect.x * scaleX,
          top: Math.max(0, rect.y * scaleY - 22),
        }}
      >
        {label}
      </div>
    </>
  );
}

function parseOptionalInt(value: string): number | undefined {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : undefined;
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value));
}

function findPortraitSourcePreview(name: string, sources: Record<string, string>): string | null {
  const suffix = `/${name}`;
  for (const [key, url] of Object.entries(sources)) {
    if (key.endsWith(suffix)) {
      return url;
    }
  }

  return null;
}
