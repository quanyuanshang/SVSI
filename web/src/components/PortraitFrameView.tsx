import { useMemo } from "react";
import {
  buildPortraitFrameStyle,
  computePortraitFrameRect,
  type PortraitGridOverride,
} from "../lib/stardewAssets";
import { usePortraitGrid } from "../stardew-ui/usePortraitGrid";

interface PortraitFrameViewProps {
  hdSheetUrl: string;
  baseSheetUrl?: string | null;
  expressionIndex?: number;
  displaySize: number;
  gridOverride?: PortraitGridOverride | null;
  className?: string;
}

/** Renders one face frame from a Stardew/Portraiture portrait sheet via CSS crop. */
export function PortraitFrameView({
  hdSheetUrl,
  baseSheetUrl,
  expressionIndex = 0,
  displaySize,
  gridOverride,
  className,
}: PortraitFrameViewProps) {
  const { grid, hdSize } = usePortraitGrid(hdSheetUrl, baseSheetUrl, gridOverride);
  const frame = useMemo(
    () => (grid ? computePortraitFrameRect(expressionIndex, grid) : null),
    [expressionIndex, grid],
  );
  const frameStyle = frame
    ? buildPortraitFrameStyle(hdSheetUrl, frame, displaySize, hdSize)
    : null;

  if (!frameStyle) {
    return (
      <span
        className={["portrait-frame-host", "portrait-frame-host--loading", className]
          .filter(Boolean)
          .join(" ")}
        style={{ width: displaySize, height: displaySize }}
      />
    );
  }

  return (
    <span
      className={["portrait-frame-host", className].filter(Boolean).join(" ")}
      style={{ width: displaySize, height: displaySize }}
    >
      <span aria-hidden="true" className="portrait-frame" style={frameStyle} />
    </span>
  );
}
