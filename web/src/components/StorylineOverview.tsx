import { CharacterPortrait } from "./CharacterPortrait";
import { EventNodeCard } from "./EventNodeCard";
import { PagePanel } from "./PagePanel";
import { StardewButton } from "./StardewButton";
import { StardewSpriteIcon } from "./StardewSpriteIcon";
import {
  buildStorylineSections,
  type StoryEventNode,
  type StoryGraph,
} from "../lib/storyGraph";
import { translateCharacter } from "../lib/translations";

interface StorylineOverviewProps {
  graph: StoryGraph;
  scopedNodes: StoryEventNode[];
  characterName: string;
  onBack: () => void;
  onSelectNode: (node: StoryEventNode) => void;
}

export function StorylineOverview({
  graph,
  scopedNodes,
  characterName,
  onBack,
  onSelectNode,
}: StorylineOverviewProps) {
  const sections = buildCharacterSections(graph, scopedNodes);
  const character = translateCharacter(characterName).zh;

  return (
    <PagePanel variant="main">
      <section className="storyline-overview">
        <div className="character-hero-row">
          <div className="character-hero">
            <span className="character-hero-portraitFrame" aria-hidden="true">
              <StardewSpriteIcon
                className="character-hero-portraitFrame__sprite"
                fallback=""
                size={112}
                spriteKey="ui.shop.portraitBackground"
              />
              <CharacterPortrait
                displaySize={88}
                name={characterName}
                shape="square"
                size="lg"
              />
            </span>
            <div>
              <p className="eyebrow">Character Storyline</p>
              <h2>{character}</h2>
              <p>故事线总览 · 查看相关事件的推进路径与前置依赖，帮助你规划下一步行动。</p>
            </div>
          </div>
          <StardewButton
            className="journal-back-button storyline-back-button"
            onClick={onBack}
            tone="quiet"
            type="button"
          >
            返回今日任务板
          </StardewButton>
        </div>

        <div className="board-legend">
          <StorylineLegend tone="recent" text="绿色 = 最近触发" />
          <StorylineLegend tone="later" text="黄色 = 现在可推进" />
          <StorylineLegend tone="next" text="蓝色 = 下一步候选" />
          <StorylineLegend tone="locked" text="红色 = 前置未满足" />
        </div>

        <div className="storyline-path">
          <StorylineColumn
            title="最近触发"
            tone="recent"
            nodes={sections.recent}
            onSelectNode={onSelectNode}
          />
          <StorylineColumn
            title="现在可推进"
            tone="later"
            nodes={sections.current}
            onSelectNode={onSelectNode}
          />
          <StorylineColumn
            title="下一步候选"
            tone="next"
            nodes={sections.next}
            onSelectNode={onSelectNode}
          />
          <StorylineColumn
            title="前置未满足"
            tone="locked"
            nodes={sections.locked}
            onSelectNode={onSelectNode}
          />
        </div>
      </section>
    </PagePanel>
  );
}

function StorylineColumn({
  title,
  tone,
  nodes,
  onSelectNode,
}: {
  title: string;
  tone: "recent" | "later" | "next" | "locked";
  nodes: StoryEventNode[];
  onSelectNode: (node: StoryEventNode) => void;
}) {
  return (
    <section className={`storyline-column storyline-column--${tone}`}>
      <div className="storyline-column__header">
        <h3>
          <StardewSpriteIcon
            fallback={storylineFallback(tone)}
            size={22}
            spriteKey={storylineSpriteKey(tone)}
          />
          {title}
        </h3>
        <span>{nodes.length}</span>
      </div>
      {nodes.length === 0 ? (
        <p className="empty-state">暂无事件。</p>
      ) : (
        <div className="storyline-column__nodes">
          {nodes.slice(0, 6).map((node) => (
            <EventNodeCard
              key={`${tone}-${node.key}`}
              node={node}
              onSelectNode={onSelectNode}
              tone={tone === "recent" ? "recent" : tone}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function StorylineLegend({
  tone,
  text,
}: {
  tone: "recent" | "later" | "next" | "locked";
  text: string;
}) {
  return (
    <span className={`legend-pill legend-pill--${tone}`}>
      <StardewSpriteIcon
        fallback={storylineFallback(tone)}
        size={18}
        spriteKey={storylineSpriteKey(tone)}
      />
      {text}
    </span>
  );
}

function storylineSpriteKey(tone: "recent" | "later" | "next" | "locked"): string {
  switch (tone) {
    case "recent":
      return "ui.shop.itemRowBackground";
    case "later":
      return "icon.scrollDown";
    case "next":
      return "ui.scrollBar.front";
    case "locked":
      return "ui.scrollBar.back";
  }
}

function storylineFallback(tone: "recent" | "later" | "next" | "locked"): string {
  switch (tone) {
    case "recent":
      return "=";
    case "later":
      return "v";
    case "next":
      return "|";
    case "locked":
      return "!";
  }
}

function buildCharacterSections(graph: StoryGraph, scopedNodes: StoryEventNode[]) {
  const base = buildStorylineSections(graph, scopedNodes);
  const recent = base.latestTriggered ? [base.latestTriggered] : base.triggered.slice(-1);
  const current = scopedNodes.filter((node) => node.status === "Current" && !node.isBlocked);
  const lockedKeys = new Set<string>();
  const next = new Map<string, StoryEventNode>();

  for (const node of [...recent, ...current]) {
    for (const dependent of node.dependents) {
      if (!dependent.node) {
        continue;
      }

      if (dependent.node.status === "Locked" || dependent.node.isBlocked) {
        lockedKeys.add(dependent.node.key);
      } else if (dependent.node.status !== "Triggered") {
        next.set(dependent.node.key, dependent.node);
      }
    }
  }

  const availableLater = scopedNodes.filter((node) => node.status === "AvailableLater");
  for (const node of availableLater) {
    next.set(node.key, node);
  }

  const locked = scopedNodes.filter(
    (node) => node.status === "Locked" || node.isBlocked || lockedKeys.has(node.key),
  );

  return {
    recent,
    current,
    next: Array.from(next.values()).filter((node) => !locked.some((item) => item.key === node.key)),
    locked,
  };
}
