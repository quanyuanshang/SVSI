import { CharacterPortrait, CharacterPortraitStack } from "./CharacterPortrait";
import { EventNodeCard } from "./EventNodeCard";
import { PagePanel } from "./PagePanel";
import { StardewButton } from "./StardewButton";
import { StardewNineSlicePanel } from "./StardewNineSlicePanel";
import { StardewSpriteIcon } from "./StardewSpriteIcon";
import {
  buildEventDependencySections,
  type StoryEventNode,
  type StoryGraph,
} from "../lib/storyGraph";
import {
  formatLocationZh,
  formatStatusReasonZh,
  formatTimeRangeZh,
} from "../lib/format";
import {
  buildGameStateFromRuntime,
  diagnoseEventTrigger,
  formatDiagnosticZh,
} from "../lib/triggerDiagnosis";
import type { RuntimeGameState } from "../types/story";

interface EventDetailViewProps {
  graph: StoryGraph;
  node: StoryEventNode;
  runtimeState?: RuntimeGameState | null;
  availableEventIds: ReadonlySet<string>;
  onBack: () => void;
  onSelectNode: (node: StoryEventNode) => void;
}

export function EventDetailView({
  graph,
  node,
  runtimeState,
  availableEventIds,
  onBack,
  onSelectNode,
}: EventDetailViewProps) {
  const dependency = buildEventDependencySections(graph, node.eventId ?? node.key);
  const diagnosis = diagnoseEventTrigger(
    node.source,
    buildGameStateFromRuntime(runtimeState),
    { availableEventIds },
  );

  return (
    <PagePanel variant="main">
      <section className="detail-page">
        <div className="detail-page__topline">
          <StardewButton className="journal-back-button" onClick={onBack} tone="quiet" type="button">
            返回今日行动板
          </StardewButton>
          <div className="journal-title">
            <p className="eyebrow">Event Detail</p>
            <h2>事件详情 / 剧情链</h2>
            <p>查看事件触发脉络与条件，追踪故事发展</p>
          </div>
        </div>

        <div className="event-chain">
          <ChainLane title="旧事件 / 更早前事件" nodes={collectOlderNodes(node)} onSelectNode={onSelectNode} />
          <ChainLane
            title="前置事件"
            nodes={dependency.upstream.map((item) => item.node).filter(isStoryEventNode)}
            unresolved={dependency.upstream.filter((item) => !item.node).map((item) => item.eventId)}
            onSelectNode={onSelectNode}
          />
          <section className="chain-lane chain-lane--focus">
            <h3>{node.isBlocked ? "当前事件（被阻止）" : "当前事件"}</h3>
            <EventNodeCard
              density="compact"
              node={node}
              onSelectNode={onSelectNode}
              showStatusText
              tone={node.isBlocked ? "locked" : "ready"}
              selected
            />
          </section>
          <ChainLane
            title="后续事件"
            nodes={dependency.downstream.map((item) => item.node).filter(isStoryEventNode)}
            onSelectNode={onSelectNode}
            muted
          />
        </div>

        <div className="detail-workbench">
          <StardewNineSlicePanel as="section" className="notebook-card" variant="note">
            <div className="notebook-card__binding" aria-hidden="true" />
            <div className="notebook-card__content">
              <h3>触发条件清单</h3>
              <ConditionChecklist
                satisfied={[
                  ...diagnosis.satisfied.map(formatDiagnosticZh),
                  ...node.resolvedConditions,
                ]}
                unsatisfied={[
                  ...diagnosis.unsatisfied.map(formatDiagnosticZh),
                  ...node.unmetConditions,
                ]}
                unknown={[
                  ...diagnosis.unknown.map(formatDiagnosticZh),
                  ...node.unresolvedConditions,
                ]}
              />
              <details className="debug-details">
                <summary>展开 raw / debug 信息</summary>
                <pre>{JSON.stringify(node.source, null, 2)}</pre>
              </details>
            </div>
          </StardewNineSlicePanel>

          <StardewNineSlicePanel as="section" className="detail-side-card" variant="textbox">
            <h3>出场角色</h3>
            <div className="cast-list">
              <div className="cast-member">
                <CharacterPortrait label="农夫" size="lg" />
                <span>玩家（自己）</span>
              </div>
              {node.characters.map((name) => (
                <div className="cast-member" key={name}>
                  <CharacterPortrait
                    name={name}
                    sourceModId={node.source.sourceModId}
                    size="lg"
                  />
                  <span>{name}</span>
                </div>
              ))}
            </div>
          </StardewNineSlicePanel>

          <StardewNineSlicePanel as="section" className="detail-side-card" variant="textbox">
            <h3>后续解锁内容</h3>
            {node.dependents.length === 0 ? (
              <p className="empty-state">当前索引中暂未发现后续依赖事件。</p>
            ) : (
              <ul className="unlock-list">
                {node.dependents.slice(0, 5).map((dependent) => (
                  <li key={`${dependent.eventId}-${dependent.node?.key ?? "missing"}`}>
                    <strong>{dependent.eventId}</strong>
                    <span>{dependent.node?.modName ?? "partial graph"}</span>
                  </li>
                ))}
              </ul>
            )}
          </StardewNineSlicePanel>
        </div>

        {node.isBlocked ? (
          <div className="blocked-footer">
            {node.blockReason ?? `当前事件已被阻止：${formatStatusReasonZh(node.statusReason, node.source)}`}
          </div>
        ) : null}
      </section>
    </PagePanel>
  );
}

function ChainLane({
  title,
  nodes,
  unresolved = [],
  muted = false,
  onSelectNode,
}: {
  title: string;
  nodes: StoryEventNode[];
  unresolved?: string[];
  muted?: boolean;
  onSelectNode: (node: StoryEventNode) => void;
}) {
  return (
    <section className={`chain-lane${muted ? " chain-lane--muted" : ""}`}>
      <h3>{title}</h3>
      {nodes.length === 0 && unresolved.length === 0 ? (
        <p className="empty-state">暂无事件。</p>
      ) : (
        <div className="chain-lane__nodes">
          {nodes.map((item) => (
            <EventNodeCard
              density="compact"
              key={`${title}-${item.key}`}
              node={item}
              onSelectNode={onSelectNode}
              tone={muted ? "neutral" : "next"}
            />
          ))}
          {unresolved.map((eventId) => (
            <div className="unresolved-node" key={`${title}-${eventId}`}>
              <strong>{eventId}</strong>
              <span>未在当前索引中找到完整节点</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

function ConditionChecklist({
  satisfied,
  unsatisfied,
  unknown,
}: {
  satisfied: string[];
  unsatisfied: string[];
  unknown: string[];
}) {
  return (
    <ul className="condition-checklist">
      {satisfied.map((item) => (
        <li className="condition-checklist__item condition-checklist__item--ok" key={`ok-${item}`}>
          <StardewSpriteIcon className="checkmark" fallback="✓" size={16} spriteKey="icon.check" />
          <span>{item}</span>
        </li>
      ))}
      {unsatisfied.map((item) => (
        <li className="condition-checklist__item condition-checklist__item--bad" key={`bad-${item}`}>
          <StardewSpriteIcon className="checkmark" fallback="□" size={16} spriteKey="icon.lock" />
          <span>{item}</span>
        </li>
      ))}
      {unknown.map((item) => (
        <li className="condition-checklist__item condition-checklist__item--unknown" key={`unknown-${item}`}>
          <StardewSpriteIcon className="checkmark" fallback="?" size={16} spriteKey="icon.warning" />
          <span>未解析条件：{item}</span>
        </li>
      ))}
    </ul>
  );
}

function collectOlderNodes(node: StoryEventNode): StoryEventNode[] {
  const older = new Map<string, StoryEventNode>();
  for (const prerequisite of node.prerequisites) {
    for (const upstream of prerequisite.node?.prerequisites ?? []) {
      if (upstream.node) {
        older.set(upstream.node.key, upstream.node);
      }
    }
  }
  return Array.from(older.values()).slice(0, 3);
}

function isStoryEventNode(node: StoryEventNode | null): node is StoryEventNode {
  return Boolean(node);
}
