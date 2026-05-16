export type StardewSpriteKind =
  | "icon"
  | "borderPatch"
  | "backgroundPatch"
  | "scrollbarPatch"
  | "tile"
  | "unknown";

export function getSpriteKindHint(kind: StardewSpriteKind): string {
  switch (kind) {
    case "icon":
      return "这是普通小图标，适合作为 icon 预览。";
    case "borderPatch":
      return "这是 UI 边框 patch，不一定像普通图标；在浅色背景上可能几乎看不见。";
    case "backgroundPatch":
      return "这是 UI 背景 patch，不一定像普通图标；可能大部分是透明或浅色。";
    case "scrollbarPatch":
      return "这是滚动条 patch，尺寸很小，放大后才容易辨认。";
    case "tile":
      return "这是 tile 片段，通常用于面板平铺而非独立图标。";
    case "unknown":
      return "类型未确认，需要人工在 atlas 上核对 rect。";
  }
}

export function inferSpriteKind(spriteKey: string, explicit?: StardewSpriteKind): StardewSpriteKind {
  if (explicit) {
    return explicit;
  }

  const key = spriteKey.toLowerCase();
  if (key.startsWith("icon.")) {
    return "icon";
  }
  if (key.includes("scrollbar") || key.includes("scroll")) {
    return key.includes("icon.") ? "icon" : "scrollbarPatch";
  }
  if (key.includes("border") || key.includes("windowborder")) {
    return "borderPatch";
  }
  if (key.includes("background") || key.includes("itemrow") || key.includes("itemicon")) {
    return "backgroundPatch";
  }
  if (key.includes("tile") || key.includes("dialogue")) {
    return "tile";
  }

  return "unknown";
}
