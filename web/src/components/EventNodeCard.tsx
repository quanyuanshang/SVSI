import { CharacterPortrait, CharacterPortraitStack } from "./CharacterPortrait";
import { StardewBadge } from "./StardewBadge";
import { StardewSpriteIcon } from "./StardewSpriteIcon";
import {
  formatLocationZh,
  formatTimeRangeZh,
} from "../lib/format";
import { translateCharacter } from "../lib/translations";
import type { StoryEventNode } from "../lib/storyGraph";

interface EventNodeCardProps {
  node: StoryEventNode;
  tone?: "ready" | "later" | "locked" | "recent" | "next" | "neutral";
  selected?: boolean;
  showStatusText?: boolean;
  onSelectNode: (node: StoryEventNode) => void;
}

export function EventNodeCard({
  node,
  tone = "neutral",
  selected = false,
  showStatusText = false,
  onSelectNode,
}: EventNodeCardProps) {
  const mainCharacter = node.characters[0];
  const hint = buildSummaryHint(node);
  const spriteKey = statusSpriteKey(node, tone);

  return (
    <button
      className={`event-node-card event-node-card--${tone}${selected ? " event-node-card--selected" : ""}`}
      onClick={() => onSelectNode(node)}
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
        <span className="event-node-card__id">ID: {node.eventId ?? "未知"}</span>
        <span>来源: {node.modName ?? "未知 Mod"}</span>
        <span>地点: {formatLocationZh(node.location, node.source.sourceModId)}</span>
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
          tone={node.isBlocked ? "locked" : "ready"}
        >
          {statusLabel(node)}
        </StardewBadge>
      ) : null}
    </button>
  );
}

function statusSpriteKey(
  node: StoryEventNode,
  tone: "ready" | "later" | "locked" | "recent" | "next" | "neutral",
): string {
  if (node.isBlocked || tone === "locked" || node.status === "Locked") {
    return "ui.scrollBar.back";
  }

  if (tone === "later" || node.status === "AvailableLater") {
    return "icon.scrollDown";
  }

  if (tone === "next") {
    return "ui.scrollBar.front";
  }

  if (tone === "recent" || node.status === "Triggered") {
    return "ui.shop.itemRowBackground";
  }

  if (tone === "ready" || node.status === "Current") {
    return "ui.shop.itemIconBackground";
  }

  return "ui.windowBorder.default";
}

function statusFallback(
  node: StoryEventNode,
  tone: "ready" | "later" | "locked" | "recent" | "next" | "neutral",
): string {
  if (node.isBlocked || tone === "locked" || node.status === "Locked") {
    return "!";
  }

  if (tone === "later" || node.status === "AvailableLater") {
    return "v";
  }

  if (tone === "next") {
    return "|";
  }

  if (tone === "recent" || node.status === "Triggered") {
    return "=";
  }

  return "*";
}

export function buildSummaryHint(node: StoryEventNode): string {
  if (node.isBlocked && node.blockReason) {
    return node.blockReason;
  }

  const missing = node.unmetConditions[0];
  if (missing) {
    return missing;
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
