import { useEffect, useRef, useState } from "react";
import type { StardewRect } from "../lib/stardewAssets";

interface SpriteCropCanvasProps {
  atlasUrl: string;
  rect: StardewRect;
  scale?: number;
  className?: string;
}

export function SpriteCropCanvas({
  atlasUrl,
  rect,
  scale = 4,
  className,
}: SpriteCropCanvasProps) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [message, setMessage] = useState("");

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }

    setStatus("loading");
    setMessage("");

    const image = new Image();
    image.onload = () => {
      const ctx = canvas.getContext("2d");
      if (!ctx) {
        setStatus("error");
        setMessage("Canvas 2D context unavailable");
        return;
      }

      const outW = Math.max(1, Math.round(rect.w * scale));
      const outH = Math.max(1, Math.round(rect.h * scale));
      canvas.width = outW;
      canvas.height = outH;
      ctx.clearRect(0, 0, outW, outH);
      ctx.imageSmoothingEnabled = false;

      try {
        ctx.drawImage(
          image,
          rect.x,
          rect.y,
          rect.w,
          rect.h,
          0,
          0,
          outW,
          outH,
        );
        setStatus("ready");
      } catch (error) {
        setStatus("error");
        setMessage(String(error));
      }
    };

    image.onerror = () => {
      setStatus("error");
      setMessage(`Failed to load atlas: ${atlasUrl}`);
    };

    image.src = atlasUrl;
  }, [atlasUrl, rect.x, rect.y, rect.w, rect.h, scale]);

  return (
    <div className={["sprite-crop-canvas", className].filter(Boolean).join(" ")}>
      <canvas className="sprite-crop-canvas__surface" ref={canvasRef} />
      {status === "loading" ? <span className="sprite-crop-canvas__status">加载 atlas…</span> : null}
      {status === "error" ? (
        <span className="sprite-crop-canvas__status sprite-crop-canvas__status--error">{message}</span>
      ) : null}
    </div>
  );
}
