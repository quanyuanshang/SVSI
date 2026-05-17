import { EventNodeCard } from "./EventNodeCard";
import { PagePanel } from "./PagePanel";
import { StorySectionPanel, type StorySectionTone } from "./StorySectionPanel";
import { StardewSpriteIcon } from "./StardewSpriteIcon";
import {
  buildTodayActionGroups,
  type StoryEventNode,
  type TodayActionGroups,
} from "../lib/storyGraph";
import { formatStardewDate } from "../lib/format";
import type { RuntimeGameState } from "../types/story";

interface ActionBoardProps {
  runtimeState?: RuntimeGameState | null;
  nodes: StoryEventNode[];
  totalCount: number;
  onSelectNode: (node: StoryEventNode) => void;
}

const BOARD_GROUPS: Array<{
  key: keyof Pick<TodayActionGroups, "ready" | "later" | "locked" | "incomplete">;
  tone: StorySectionTone;
  cardTone: "ready" | "later" | "locked" | "unknown";
  title: string;
  legend: string;
  iconKey: string;
  fallbackIcon: string;
}> = [
  {
    key: "ready",
    tone: "current",
    cardTone: "ready",
    title: "现在可触发",
    legend: "绿色 = 现在就能推进",
    iconKey: "ui.shop.itemIconBackground",
    fallbackIcon: "*",
  },
  {
    key: "later",
    tone: "later",
    cardTone: "later",
    title: "稍后可触发",
    legend: "黄色 = 换时间 / 地点 / 天气再来",
    iconKey: "icon.scrollDown",
    fallbackIcon: "v",
  },
  {
    key: "locked",
    tone: "locked",
    cardTone: "locked",
    title: "前置未满足",
    legend: "红色 = 先补前置剧情或条件",
    iconKey: "ui.scrollBar.back",
    fallbackIcon: "!",
  },
  {
    key: "incomplete",
    tone: "unknown",
    cardTone: "unknown",
    title: "条件未知",
    legend: "灰蓝色 = 条件无法静态判断",
    iconKey: "icon.warning",
    fallbackIcon: "?",
  },
];

export function ActionBoard({
  runtimeState,
  nodes,
  totalCount,
  onSelectNode,
}: ActionBoardProps) {
  const groups = buildTodayActionGroups(nodes);

  return (
    <PagePanel variant="main">
      <section className="action-board">
      <div className="journal-title">
        <p className="eyebrow">Today Action Board</p>
        <h2>今日行动板</h2>
        <p>{formatStardewDate(runtimeState)} · 当前显示 {nodes.length} / 总计 {totalCount}</p>
      </div>

      <div className="board-legend" aria-label="状态颜色说明">
        {BOARD_GROUPS.map((group) => (
          <span className={`legend-pill legend-pill--${group.tone}`} key={group.key}>
            <StardewSpriteIcon
              fallback={group.fallbackIcon}
              size={18}
              spriteKey={group.iconKey}
            />
            {group.legend}
          </span>
        ))}
      </div>

      <div className="action-board__sections">
        {BOARD_GROUPS.map((group) => (
          <ActionBoardSection
            key={group.key}
            nodes={groups[group.key]}
            onSelectNode={onSelectNode}
            fallbackIcon={group.fallbackIcon}
            iconKey={group.iconKey}
            title={group.title}
            tone={group.tone}
            cardTone={group.cardTone}
          />
        ))}
      </div>
      </section>
    </PagePanel>
  );
}

function ActionBoardSection({
  title,
  tone,
  iconKey,
  fallbackIcon,
  nodes,
  onSelectNode,
  cardTone,
}: {
  title: string;
  tone: StorySectionTone;
  cardTone: "ready" | "later" | "locked" | "unknown";
  iconKey: string;
  fallbackIcon: string;
  nodes: StoryEventNode[];
  onSelectNode: (node: StoryEventNode) => void;
}) {
  return (
    <StorySectionPanel
      count={nodes.length}
      fallbackIcon={fallbackIcon}
      iconKey={iconKey}
      title={title}
      tone={tone}
    >
      {nodes.length === 0 ? (
        <p className="empty-state">这一组暂时没有事件。</p>
      ) : (
        <div className="event-node-grid">
          {nodes.slice(0, 8).map((node) => (
            <EventNodeCard
              key={`${tone}-${node.key}`}
              node={node}
              onSelectNode={onSelectNode}
              tone={cardTone}
            />
          ))}
          {nodes.length > 8 ? (
            <p className="empty-state">还有 {nodes.length - 8} 个事件，已按后续解锁价值排序。</p>
          ) : null}
        </div>
      )}
    </StorySectionPanel>
  );
}
