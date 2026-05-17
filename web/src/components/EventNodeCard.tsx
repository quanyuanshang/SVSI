import type { CSSProperties } from "react";
import { CharacterPortrait, CharacterPortraitStack } from "./CharacterPortrait";
import { StardewBadge } from "./StardewBadge";
import { StardewSpriteIcon } from "./StardewSpriteIcon";
import {
  formatLocationZh,
  formatTimeRangeZh,
} from "../lib/format";
import { useStardewAssetResolver } from "../lib/stardewAssets";
import { translateCharacter } from "../lib/translations";
import type { StoryEventNode } from "../lib/storyGraph";

interface EventNodeCardProps {
  node: StoryEventNode;
  tone?: EventNodeCardTone;
  density?: "default" | "compact";
  selected?: boolean;
  showStatusText?: boolean;
  onSelectNode: (node: StoryEventNode) => void;
}

type EventNodeCardTone =
  | "ready"
  | "current"
  | "later"
  | "locked"
  | "unknown"
  | "triggered"
  | "recent"
  | "next"
  | "neutral";

export function EventNodeCard({
  node,
  tone = "neutral",
  density = "default",
  selected = false,
  showStatusText = false,
  onSelectNode,
}: EventNodeCardProps) {
  const mainCharacter = node.characters[0];
  const hint = buildSummaryHint(node);
  const spriteKey = statusSpriteKey(node, tone);
  const eventBoardStyle = useEventBoardBackgroundStyle();

  return (
    <button
      className={`event-node-card event-node-card--${tone} event-node-card--${density}${selected ? " event-node-card--selected" : ""}`}
      onClick={() => onSelectNode(node)}
      style={eventBoardStyle}
      title={hint}
      type="button"
    >
      <span className="event-node-card__sprite-corner" aria-hidden="true">
        <StardewSpriteIcon
          fallback={statusFallback(node, tone)}
          size={32}
          spriteKey={spriteKey}
        />
      </span>
      <CharacterPortrait
        name={mainCharacter}
        sourceModId={node.source.sourceModId}
        size="lg"
      />
      <span className="event-node-card__content">
        <span className="event-node-card__title">{node.displayName}</span>
        {density === "compact" ? null : (
          <span className="event-node-card__meta event-node-card__id">ID: {node.eventId ?? "未知"}</span>
        )}
        <span className="event-node-card__meta">来源: {node.modName ?? "未知 Mod"}</span>
        <span className="event-node-card__meta">地点: {formatLocationZh(node.location, node.source.sourceModId)}</span>
        <CharacterPortraitStack
          names={node.characters}
          sourceModId={node.source.sourceModId}
        />
        {hint ? (
          <span className="event-node-card__hint">
            <StardewSpriteIcon
              fallback={node.isBlocked ? "!" : "v"}
              size={16}
              spriteKey={node.isBlocked ? "ui.scrollBar.back" : "icon.scrollDown"}
            />
            {hint}
          </span>
        ) : null}
      </span>
      {showStatusText ? (
        <StardewBadge
          className="event-node-card__status"
          fallbackIcon={statusFallback(node, tone)}
          iconKey={spriteKey}
          tone={badgeTone(node, tone)}
        >
          {statusLabel(node)}
        </StardewBadge>
      ) : null}
    </button>
  );
}

function useEventBoardBackgroundStyle(): CSSProperties | undefined {
  const resolver = useStardewAssetResolver();
  const sprite =
    resolver.getSprite("icon.eventBoard") ??
    resolver.getSprite("icon.eventboard") ??
    resolver.getSprite("ui.eventboard") ??
    resolver.getSprite("ui.board.eventboard");

  if (!sprite?.atlasUrl) {
    return undefined;
  }

  return {
    "--event-node-card-board-image": `url("${sprite.atlasUrl}")`,
  } as CSSProperties;
}

function statusSpriteKey(
  node: StoryEventNode,
  tone: EventNodeCardTone,
): string {
  if (node.isBlocked || tone === "locked" || node.status === "Locked") {
    return "ui.scrollBar.back";
  }

  if (tone === "unknown" || node.status === "Unknown") {
    return "icon.warning";
  }

  if (tone === "later" || node.status === "AvailableLater") {
    return "icon.scrollDown";
  }

  if (tone === "next") {
    return "ui.scrollBar.front";
  }

  if (tone === "triggered" || tone === "recent" || node.status === "Triggered") {
    return "ui.shop.itemRowBackground";
  }

  if (tone === "ready" || tone === "current" || node.status === "Current") {
    return "ui.shop.itemIconBackground";
  }

  return "icon.eventBoard";
}

function statusFallback(
  node: StoryEventNode,
  tone: EventNodeCardTone,
): string {
  if (node.isBlocked || tone === "locked" || node.status === "Locked") {
    return "!";
  }

  if (tone === "unknown" || node.status === "Unknown") {
    return "?";
  }

  if (tone === "later" || node.status === "AvailableLater") {
    return "v";
  }

  if (tone === "next") {
    return "|";
  }

  if (tone === "triggered" || tone === "recent" || node.status === "Triggered") {
    return "=";
  }

  return "*";
}

function badgeTone(
  node: StoryEventNode,
  tone: EventNodeCardTone,
): "ready" | "later" | "locked" | "neutral" {
  if (node.isBlocked || tone === "locked" || node.status === "Locked") {
    return "locked";
  }

  if (tone === "later" || node.status === "AvailableLater") {
    return "later";
  }

  if (tone === "unknown" || node.status === "Unknown") {
    return "neutral";
  }

  return "ready";
}

export function buildSummaryHint(node: StoryEventNode): string {
  if (node.isBlocked && node.blockReason) {
    return node.blockReason;
  }

  const missing = node.unmetConditions[0];
  if (missing) {
    return missing;
  }

  const unresolved = node.unresolvedConditions[0];
  if (unresolved) {
    return `未知条件：${unresolved}`;
  }

  if (node.timeWindow) {
    return `推荐时间：${formatTimeRangeZh(node.timeWindow.start, node.timeWindow.end)}`;
  }

  if (node.characters[0]) {
    return `相关角色：${translateCharacter(node.characters[0], node.source.sourceModId).zh}`;
  }

  return "";
}

function statusLabel(node: StoryEventNode): string {
  if (node.isBlocked) {
    return "被阻止";
  }

  switch (node.status) {
    case "Current":
      return "现在可触发";
    case "AvailableLater":
      return "稍后可触发";
    case "Locked":
      return "前置未满足";
    case "Triggered":
      return "已触发";
    default:
      return "条件未明";
  }
}
