import type { PointerEvent, ReactNode } from "react";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  buildSpriteBackgroundStyle,
  probeAssetUrl,
  type ResolvedSprite,
  type StardewRect,
  useAtlasNaturalSize,
  useStardewAssetResolver,
} from "../lib/stardewAssets";
import { getSpriteKindHint } from "../stardew-ui/stardewSpriteTypes";
import { PortraitDebugSection } from "./PortraitDebugSection";
import { SpriteCropCanvas } from "./SpriteCropCanvas";
import { StardewSpriteIcon } from "./StardewSpriteIcon";

type DragState = { startX: number; startY: number };
type UrlProbe = { url: string; ok: boolean; status: number | null; contentType: string | null };

const EMPTY_RECT: StardewRect = { x: 0, y: 0, w: 16, h: 16 };
const CURSORS_PROBE_URL = "/generated/stardew-ui/LooseSprites/Cursors.png";

export function StardewAssetsDebugPage() {
  const resolver = useStardewAssetResolver();
  const sprites = resolver.manifest?.sprites ?? {};
  const assets = resolver.manifest?.assets ?? {};
  const spriteEntries = Object.entries(sprites).sort(([a], [b]) => a.localeCompare(b));
  const assetEntries = Object.entries(assets);

  const [selectedSpriteKey, setSelectedSpriteKey] = useState("icon.scrollDown");
  const [selectedAssetKey, setSelectedAssetKey] = useState("LooseSprites/Cursors");
  const [rect, setRect] = useState<StardewRect>(EMPTY_RECT);
  const [hoverPoint, setHoverPoint] = useState<{ x: number; y: number } | null>(null);
  const [spriteKeyInput, setSpriteKeyInput] = useState("icon.scrollDown");
  const [notes, setNotes] = useState("Manually selected from atlas.");
  const [nineSlice, setNineSlice] = useState({ top: "", right: "", bottom: "", left: "" });
  const [drag, setDrag] = useState<DragState | null>(null);
  const [urlProbe, setUrlProbe] = useState<UrlProbe | null>(null);
  const imageRef = useRef<HTMLImageElement | null>(null);

  const selectedAsset = assets[selectedAssetKey];
  const atlasSize = useAtlasNaturalSize(selectedAsset?.url);
  const resolvedSprite = resolver.getSprite(selectedSpriteKey);

  useEffect(() => {
    void probeAssetUrl(CURSORS_PROBE_URL).then(setUrlProbe);
  }, []);

  useEffect(() => {
    const sprite = sprites[selectedSpriteKey];
    if (!sprite) {
      return;
    }

    setSelectedAssetKey(sprite.asset);
    setRect(sprite.rect);
    setSpriteKeyInput(selectedSpriteKey);
    if (sprite.notes) {
      setNotes(sprite.notes);
    }
  }, [selectedSpriteKey, sprites]);

  const previewSprite: ResolvedSprite | null = selectedAsset
    ? {
        asset: selectedAssetKey,
        assetKey: selectedAssetKey,
        atlasKey: selectedAssetKey,
        atlasUrl: selectedAsset.url,
        assetType: selectedAsset.type,
        rect,
        source: "manual:/stardew-assets-debug",
        confidence: "medium",
        notes,
        spriteKind: resolvedSprite?.spriteKind ?? "unknown",
      }
    : null;

  const exportJson = useMemo(
    () => buildExportJson(spriteKeyInput, selectedAssetKey, rect, nineSlice, notes, resolvedSprite?.type),
    [spriteKeyInput, selectedAssetKey, rect, nineSlice, notes, resolvedSprite?.type],
  );

  const cssScaledStyle =
    previewSprite && atlasSize
      ? buildSpriteBackgroundStyle(previewSprite, {
          scale: 4,
          atlasSize,
        })
      : null;

  const cssNativeStyle = previewSprite
    ? buildSpriteBackgroundStyle(previewSprite, { scale: 1, atlasSize: null })
    : null;

  const handlePointer = (event: PointerEvent<HTMLImageElement>) => {
    const point = getAtlasPoint(event.currentTarget, event.clientX, event.clientY);
    setHoverPoint(point);
    if (!drag) {
      return;
    }

    setRect(normalizeRect(drag.startX, drag.startY, point.x, point.y));
  };

  return (
    <main className="page-shell stardew-assets-debug">
      <section className="panel stardew-assets-debug__intro">
        <a className="debug-back-link" href="/">
          ? ?? Inspector
        </a>
        <div className="stardew-assets-debug__intro-copy">
          <p className="eyebrow">Stardew UI Atlas Debug</p>
          <h1>Sprite ??????</h1>
          <p>
            ?? <strong>Canvas crop</strong>????? <strong>CSS background</strong>?????????
            ? Canvas ???? CSS ?? ? CSS ???? Canvas ??? ? ????????? atlas ????
          </p>
        </div>
        <div className="debug-url-probe">
          <strong>Atlas URL ??</strong>
          <code>{CURSORS_PROBE_URL}</code>
          {urlProbe ? (
            <span className={urlProbe.ok ? "debug-ok" : "debug-warn"}>
              {urlProbe.ok
                ? `OK (${urlProbe.status ?? "?"}) ${urlProbe.contentType ?? ""}`
                : `FAIL (${urlProbe.status ?? "network"})`}
            </span>
          ) : (
            <span>????</span>
          )}
        </div>
      </section>

      <PortraitDebugSection manifest={resolver.manifest} />

      <section className="stardew-assets-debug__grid">
        <aside className="panel stardew-assets-debug__sidebar">
          <h2>Sprites (seed + local)</h2>
          <p className="debug-note">????? atlas ??? rect?</p>
          <div className="debug-sprite-list">
            {spriteEntries.map(([key, sprite]) => (
              <button
                className={key === selectedSpriteKey ? "is-selected" : ""}
                key={key}
                onClick={() => setSelectedSpriteKey(key)}
                type="button"
              >
                <strong>{key}</strong>
                <span className="debug-sprite-type">{sprite.type ?? "unknown"}</span>
                <span>
                  {sprite.asset} ? x={sprite.rect.x} y={sprite.rect.y} {sprite.rect.w}?{sprite.rect.h}
                </span>
              </button>
            ))}
          </div>

          <h3>Assets</h3>
          <div className="debug-asset-list">
            {assetEntries.map(([assetKey, asset]) => (
              <button
                className={assetKey === selectedAssetKey ? "is-selected" : ""}
                key={assetKey}
                onClick={() => {
                  setSelectedAssetKey(assetKey);
                }}
                type="button"
              >
                <strong>{assetKey}</strong>
                <span>{asset.url}</span>
              </button>
            ))}
          </div>
        </aside>

        <section className="panel stardew-assets-debug__atlas">
          <div className="debug-toolbar">
            <div>
              <h2>{selectedAssetKey}</h2>
              <p>{selectedAsset?.url ?? "?"}</p>
              {atlasSize ? (
                <p className="debug-note">
                  atlas {atlasSize.width}?{atlasSize.height}px
                </p>
              ) : (
                <p className="debug-warn">atlas ?????</p>
              )}
            </div>
            <output>
              {hoverPoint ? `cursor x:${hoverPoint.x} y:${hoverPoint.y}` : "hover atlas"}
            </output>
          </div>

          {selectedAsset ? (
            <div className="debug-atlas-frame checkerboard">
              <img
                alt={`${selectedAssetKey} atlas`}
                draggable={false}
                onPointerDown={(event) => {
                  event.currentTarget.setPointerCapture(event.pointerId);
                  const point = getAtlasPoint(event.currentTarget, event.clientX, event.clientY);
                  setDrag({ startX: point.x, startY: point.y });
                  setRect({ x: point.x, y: point.y, w: 1, h: 1 });
                }}
                onPointerLeave={() => setHoverPoint(null)}
                onPointerMove={handlePointer}
                onPointerUp={(event) => {
                  event.currentTarget.releasePointerCapture(event.pointerId);
                  setDrag(null);
                }}
                ref={imageRef}
                src={selectedAsset.url}
              />
              <AtlasRectOverlay
                image={imageRef.current}
                rect={rect}
                label={`${selectedSpriteKey} ? x=${rect.x} y=${rect.y} w=${rect.w} h=${rect.h}`}
              />
            </div>
          ) : (
            <p className="empty-state">???? atlas?</p>
          )}
        </section>

        <aside className="panel stardew-assets-debug__inspector">
          <h2>????</h2>

          {resolvedSprite ? (
            <p className={`debug-type-hint debug-type-hint--${resolvedSprite.spriteKind}`}>
              <strong>type: {resolvedSprite.spriteKind}</strong>
              <br />
              {getSpriteKindHint(resolvedSprite.spriteKind)}
            </p>
          ) : null}

          <div className="debug-preview-grid">
            <PreviewPanel title="Canvas crop????" subtitle="drawImage ??">
              {previewSprite ? (
                <SpriteCropCanvas atlasUrl={previewSprite.atlasUrl} rect={rect} scale={4} />
              ) : null}
            </PreviewPanel>

            <PreviewPanel title="CSS 1??? background-size?" subtitle="??????????">
              {cssNativeStyle ? (
                <span className="stardew-sprite-icon stardew-sprite-icon--asset checkerboard" style={cssNativeStyle} />
              ) : null}
            </PreviewPanel>

            <PreviewPanel
              title="CSS scaled?? atlas ???"
              subtitle={
                atlasSize
                  ? `scale=4 ? bg ${atlasSize.width * 4}?${atlasSize.height * 4}`
                  : "?? atlas naturalWidth/Height"
              }
            >
              {cssScaledStyle ? (
                <span className="stardew-sprite-icon stardew-sprite-icon--asset checkerboard" style={cssScaledStyle} />
              ) : (
                <span className="debug-note">atlas ???????</span>
              )}
            </PreviewPanel>

            <PreviewPanel title="StardewSpriteIcon" subtitle="????">
              <StardewSpriteIcon spriteKey={selectedSpriteKey} size={48} fallback="?" />
            </PreviewPanel>
          </div>

          <h3>?? rect / ??</h3>
          <label>
            spriteKey
            <input value={spriteKeyInput} onChange={(e) => setSpriteKeyInput(e.target.value)} />
          </label>
          <fieldset>
            <legend>Rect</legend>
            {(["x", "y", "w", "h"] as const).map((field) => (
              <label key={field}>
                {field}
                <input
                  min={field === "w" || field === "h" ? 1 : 0}
                  onChange={(e) =>
                    setRect((c) => ({ ...c, [field]: Number(e.target.value) }))
                  }
                  type="number"
                  value={rect[field]}
                />
              </label>
            ))}
          </fieldset>
          <pre>{exportJson}</pre>
        </aside>
      </section>
    </main>
  );
}

function PreviewPanel({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <div className="debug-preview-panel checkerboard">
      <header>
        <strong>{title}</strong>
        <span>{subtitle}</span>
      </header>
      <div className="debug-preview-panel__body">{children}</div>
    </div>
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

function getAtlasPoint(image: HTMLImageElement, clientX: number, clientY: number) {
  const bounds = image.getBoundingClientRect();
  const scaleX = image.naturalWidth / bounds.width;
  const scaleY = image.naturalHeight / bounds.height;

  return {
    x: clamp(Math.floor((clientX - bounds.left) * scaleX), 0, image.naturalWidth),
    y: clamp(Math.floor((clientY - bounds.top) * scaleY), 0, image.naturalHeight),
  };
}

function normalizeRect(startX: number, startY: number, endX: number, endY: number): StardewRect {
  const x = Math.min(startX, endX);
  const y = Math.min(startY, endY);

  return {
    x,
    y,
    w: Math.max(1, Math.abs(endX - startX)),
    h: Math.max(1, Math.abs(endY - startY)),
  };
}

function buildExportJson(
  spriteKey: string,
  asset: string,
  rect: StardewRect,
  nineSlice: { top: string; right: string; bottom: string; left: string },
  notes: string,
  type?: string,
) {
  const parsedNineSlice = parseNineSlice(nineSlice);
  const body = {
    asset,
    rect,
    ...(type ? { type } : {}),
    ...(parsedNineSlice ? { nineSlice: parsedNineSlice } : {}),
    source: "manual:/stardew-assets-debug",
    confidence: "medium",
    notes,
  };

  return `"${spriteKey}": ${JSON.stringify(body, null, 2)}`;
}

function parseNineSlice(value: { top: string; right: string; bottom: string; left: string }) {
  if (!value.top && !value.right && !value.bottom && !value.left) {
    return null;
  }

  return {
    top: Number(value.top || 0),
    right: Number(value.right || 0),
    bottom: Number(value.bottom || 0),
    left: Number(value.left || 0),
  };
}

function clamp(value: number, min: number, max: number) {
  return Math.max(min, Math.min(max, value));
}
