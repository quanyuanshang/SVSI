import type { CSSProperties, PointerEvent, ReactNode } from "react";
import { useEffect, useMemo, useRef, useState } from "react";
import { getAtlasPoint, normalizeRect } from "../stardew-ui/atlasCropMath";
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

const ATLAS_ZOOM_MIN = 0.25;
const ATLAS_ZOOM_MAX = 8;
const ATLAS_ZOOM_STEP = 1.15;

const PRIMARY_CURSOR_ATLASES = [
  { key: "LooseSprites/Cursors", label: "Cursors", probeUrl: "/generated/stardew-ui/LooseSprites/Cursors.png" },
  {
    key: "LooseSprites/Cursors2",
    label: "Cursors2",
    probeUrl: "/generated/stardew-ui/LooseSprites/Cursors2.png",
  },
] as const;

export function StardewAssetsDebugPage() {
  const resolver = useStardewAssetResolver();
  const sprites = resolver.manifest?.sprites;
  const assets = resolver.manifest?.assets;
  const spriteEntries = useMemo(
    () => Object.entries(sprites ?? {}).sort(([a], [b]) => a.localeCompare(b)),
    [sprites],
  );
  const assetEntries = useMemo(
    () => Object.entries(assets ?? {}).sort(([a], [b]) => a.localeCompare(b)),
    [assets],
  );

  const [selectedSpriteKey, setSelectedSpriteKey] = useState("icon.scrollDown");
  const [selectedAssetKey, setSelectedAssetKey] = useState("LooseSprites/Cursors");
  const [rect, setRect] = useState<StardewRect>(EMPTY_RECT);
  const [hoverPoint, setHoverPoint] = useState<{ x: number; y: number } | null>(null);
  const [spriteKeyInput, setSpriteKeyInput] = useState("icon.scrollDown");
  const [notes, setNotes] = useState("Manually selected from atlas.");
  const [nineSlice, setNineSlice] = useState({ top: "", right: "", bottom: "", left: "" });
  const [drag, setDrag] = useState<DragState | null>(null);
  const [urlProbes, setUrlProbes] = useState<Record<string, UrlProbe>>({});
  const [atlasZoom, setAtlasZoom] = useState(1);
  const imageRef = useRef<HTMLImageElement | null>(null);
  const atlasFrameRef = useRef<HTMLDivElement | null>(null);
  const [, refreshOverlay] = useState(0);

  const selectedSpriteDef = sprites?.[selectedSpriteKey];
  const selectedAsset = assets?.[selectedAssetKey];
  const atlasSize = useAtlasNaturalSize(selectedAsset?.url);
  const resolvedSprite = resolver.getSprite(selectedSpriteKey);

  useEffect(() => {
    void Promise.all(
      PRIMARY_CURSOR_ATLASES.map(async ({ probeUrl }) => {
        const probe = await probeAssetUrl(probeUrl);
        return [probeUrl, probe] as const;
      }),
    ).then((entries) => {
      setUrlProbes(Object.fromEntries(entries));
    });
  }, []);

  useEffect(() => {
    if (!selectedSpriteDef) {
      return;
    }

    setSelectedAssetKey(selectedSpriteDef.asset);
    setRect(selectedSpriteDef.rect);
    setSpriteKeyInput(selectedSpriteKey);
    if (selectedSpriteDef.notes) {
      setNotes(selectedSpriteDef.notes);
    }
  }, [selectedSpriteKey, selectedSpriteDef]);

  useEffect(() => {
    setAtlasZoom(1);
  }, [selectedAssetKey]);

  useEffect(() => {
    const frame = atlasFrameRef.current;
    if (!frame) {
      return;
    }

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      event.stopPropagation();

      setAtlasZoom((current) => {
        const factor = event.deltaY < 0 ? ATLAS_ZOOM_STEP : 1 / ATLAS_ZOOM_STEP;
        const next = clampAtlasZoom(current * factor);
        const ratio = next / current;

        const frameRect = frame.getBoundingClientRect();
        const cursorX = event.clientX - frameRect.left + frame.scrollLeft;
        const cursorY = event.clientY - frameRect.top + frame.scrollTop;

        requestAnimationFrame(() => {
          frame.scrollLeft = cursorX * ratio - (event.clientX - frameRect.left);
          frame.scrollTop = cursorY * ratio - (event.clientY - frameRect.top);
        });

        return next;
      });
    };

    frame.addEventListener("wheel", onWheel, { passive: false });
    return () => frame.removeEventListener("wheel", onWheel);
  }, [selectedAssetKey]);

  const resultPreviewScale = useMemo(() => {
    const maxEdge = Math.max(rect.w, rect.h, 1);
    return Math.max(1, Math.min(12, Math.floor(320 / maxEdge)));
  }, [rect.h, rect.w]);

  const stageSize = useMemo(() => {
    if (!atlasSize) {
      return null;
    }

    return {
      width: Math.round(atlasSize.width * atlasZoom),
      height: Math.round(atlasSize.height * atlasZoom),
    };
  }, [atlasSize, atlasZoom]);

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

  const finishDrag = (event: PointerEvent<HTMLImageElement>) => {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }
    setDrag(null);
  };

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
          返回 Inspector
        </a>
        <div className="stardew-assets-debug__intro-copy">
          <p className="eyebrow">Stardew UI Atlas Debug</p>
          <h1>Sprite 坐标标注</h1>
          <p>
            用 <strong>Canvas crop</strong> 与 <strong>CSS background</strong> 对照验证 rect。
            在 atlas 上拖拽框选区域；在图内滚轮可缩放 atlas（不缩放页面）。复制右侧 JSON 到本地 manifest。
          </p>
        </div>
        <div className="debug-url-probe">
          <strong>Atlas URL 探测</strong>
          {PRIMARY_CURSOR_ATLASES.map(({ label, probeUrl }) => {
            const probe = urlProbes[probeUrl];
            return (
              <div className="debug-url-probe__row" key={probeUrl}>
                <span>{label}</span>
                <code>{probeUrl}</code>
                {probe ? (
                  <span className={probe.ok ? "debug-ok" : "debug-warn"}>
                    {probe.ok
                      ? `OK (${probe.status ?? "?"}) ${probe.contentType ?? ""}`
                      : `FAIL (${probe.status ?? "network"})`}
                  </span>
                ) : (
                  <span>检测中…</span>
                )}
              </div>
            );
          })}
        </div>
      </section>

      <PortraitDebugSection manifest={resolver.manifest} />

      <section className="stardew-assets-debug__grid">
        <aside className="panel stardew-assets-debug__sidebar">
          <h2>Sprites (seed + local)</h2>
          <p className="debug-note">点击条目会同步 atlas 与 rect。</p>
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
                  {sprite.asset} · x={sprite.rect.x} y={sprite.rect.y} {sprite.rect.w}×{sprite.rect.h}
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
                  setDrag(null);
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
              <p>{selectedAsset?.url ?? "—"}</p>
              {atlasSize ? (
                <p className="debug-note">
                  atlas {atlasSize.width}×{atlasSize.height}px
                </p>
              ) : (
                <p className="debug-warn">atlas 尺寸未知（图片可能未生成或未加载）</p>
              )}
              <div className="debug-atlas-tabs" role="tablist" aria-label="Cursor atlases">
                {PRIMARY_CURSOR_ATLASES.map(({ key, label }) => (
                  <button
                    aria-selected={selectedAssetKey === key}
                    className={selectedAssetKey === key ? "is-selected" : ""}
                    disabled={!assets?.[key]}
                    key={key}
                    onClick={() => {
                      setSelectedAssetKey(key);
                      setDrag(null);
                    }}
                    role="tab"
                    type="button"
                  >
                    {label}
                  </button>
                ))}
              </div>
              <div className="debug-atlas-zoom" aria-label="Atlas zoom">
                <button
                  onClick={() => setAtlasZoom((value) => clampAtlasZoom(value / ATLAS_ZOOM_STEP))}
                  type="button"
                >
                  −
                </button>
                <output>{Math.round(atlasZoom * 100)}%</output>
                <button
                  onClick={() => setAtlasZoom((value) => clampAtlasZoom(value * ATLAS_ZOOM_STEP))}
                  type="button"
                >
                  +
                </button>
                <button onClick={() => setAtlasZoom(1)} type="button">
                  1:1
                </button>
                <span className="debug-note">图内滚轮缩放</span>
              </div>
            </div>
            <output>
              {hoverPoint
                ? `pointer x:${hoverPoint.x} y:${hoverPoint.y} · zoom ${Math.round(atlasZoom * 100)}%`
                : "在 atlas 上移动或拖拽"}
            </output>
          </div>

          {selectedAsset ? (
            <div className="debug-atlas-frame checkerboard" ref={atlasFrameRef}>
              <div
                className="debug-atlas-stage"
                style={
                  stageSize
                    ? { width: stageSize.width, height: stageSize.height }
                    : undefined
                }
              >
                <img
                  alt={`${selectedAssetKey} atlas`}
                  draggable={false}
                  key={selectedAssetKey}
                  onLoad={() => refreshOverlay((value) => value + 1)}
                  onPointerCancel={finishDrag}
                  onPointerDown={(event) => {
                    event.currentTarget.setPointerCapture(event.pointerId);
                    const point = getAtlasPoint(event.currentTarget, event.clientX, event.clientY);
                    setDrag({ startX: point.x, startY: point.y });
                    setRect({ x: point.x, y: point.y, w: 1, h: 1 });
                  }}
                  onPointerLeave={() => setHoverPoint(null)}
                  onPointerMove={handlePointer}
                  onPointerUp={finishDrag}
                  ref={imageRef}
                  src={selectedAsset.url}
                  style={
                    stageSize
                      ? { width: stageSize.width, height: stageSize.height }
                      : undefined
                  }
                />
                <AtlasRectOverlay
                  image={imageRef.current}
                  rect={rect}
                  label={`${spriteKeyInput} · x=${rect.x} y=${rect.y} w=${rect.w} h=${rect.h}`}
                />
              </div>
            </div>
          ) : (
            <p className="empty-state">未找到 atlas，请先运行 extract:stardew-ui。</p>
          )}
        </section>

        <aside className="panel stardew-assets-debug__inspector">
          <h2>预览与导出</h2>

          {resolvedSprite ? (
            <p className={`debug-type-hint debug-type-hint--${resolvedSprite.spriteKind}`}>
              <strong>type: {resolvedSprite.spriteKind}</strong>
              <br />
              {getSpriteKindHint(resolvedSprite.spriteKind)}
            </p>
          ) : null}

          <div className="debug-sprite-result checkerboard">
            <header className="debug-sprite-result__header">
              <strong>选区结果</strong>
              <span>
                {rect.w}×{rect.h} px
                {previewSprite ? ` · 预览 ×${resultPreviewScale}` : ""}
              </span>
            </header>
            <div className="debug-sprite-result__body">
              {previewSprite ? (
                <SpriteCropCanvas
                  atlasUrl={previewSprite.atlasUrl}
                  maxDisplaySize={320}
                  rect={rect}
                  scale={resultPreviewScale}
                />
              ) : (
                <span className="debug-note">框选 atlas 后在此查看完整 sprite</span>
              )}
            </div>
          </div>

          <div className="debug-preview-grid">
            <PreviewPanel title="Canvas crop（推荐）" subtitle="drawImage 裁切">
              {previewSprite ? (
                <SpriteCropCanvas
                  atlasUrl={previewSprite.atlasUrl}
                  maxDisplaySize={160}
                  rect={rect}
                  scale={4}
                />
              ) : null}
            </PreviewPanel>

            <PreviewPanel title="CSS 1×（background-size 未缩放）" subtitle="与游戏原生像素一致">
              {cssNativeStyle ? (
                <FitSpritePreview rect={rect} style={cssNativeStyle} />
              ) : null}
            </PreviewPanel>

            <PreviewPanel
              title="CSS scaled（按 atlas 缩放）"
              subtitle={
                atlasSize
                  ? `scale=4 · bg ${atlasSize.width * 4}×${atlasSize.height * 4}`
                  : "等待 atlas naturalWidth/Height"
              }
            >
              {cssScaledStyle ? (
                <FitSpritePreview rect={rect} style={cssScaledStyle} />
              ) : (
                <span className="debug-note">atlas 尺寸加载后可预览</span>
              )}
            </PreviewPanel>

            <PreviewPanel title="StardewSpriteIcon" subtitle="组件路径">
              <StardewSpriteIcon spriteKey={selectedSpriteKey} size={48} fallback="?" />
            </PreviewPanel>
          </div>

          <h3>编辑 rect / 导出</h3>
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
                    setRect((current) => ({ ...current, [field]: Number(e.target.value) }))
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

function FitSpritePreview({ style, rect }: { style: CSSProperties; rect: StardewRect }) {
  const width = typeof style.width === "number" ? style.width : rect.w;
  const height = typeof style.height === "number" ? style.height : rect.h;
  const maxEdge = 160;
  const fitScale = Math.min(1, maxEdge / Math.max(width, height, 1));

  return (
    <div className="debug-sprite-fit">
      <span
        className="stardew-sprite-icon stardew-sprite-icon--asset checkerboard"
        style={{
          ...style,
          transform: fitScale < 1 ? `scale(${fitScale})` : undefined,
          transformOrigin: "center center",
        }}
      />
    </div>
  );
}

function clampAtlasZoom(value: number) {
  return Math.max(ATLAS_ZOOM_MIN, Math.min(ATLAS_ZOOM_MAX, Number(value.toFixed(3))));
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
