import { extractCharactersFromNode } from "../lib/characters";
import {
  formatLocationZh,
  formatStardewDate,
  formatStatusLabel,
  formatStatusReasonZh,
  formatTimeRangeZh,
} from "../lib/format";
import { extractTimeWindow, groupNodesForTimeline } from "../lib/timeWindows";
import { translateCharacter } from "../lib/translations";
import type { RuntimeGameState, StoryNodeEvaluation } from "../types/story";

interface DayTimelineViewProps {
  runtimeState?: RuntimeGameState | null;
  nodes: StoryNodeEvaluation[];
  totalCount: number;
  selectedNodeId: string | null;
  conflictCountByNodeId: Record<string, number>;
  onSelectNode: (node: StoryNodeEvaluation) => void;
}

export function DayTimelineView({
  runtimeState,
  nodes,
  totalCount,
  selectedNodeId,
  conflictCountByNodeId,
  onSelectNode,
}: DayTimelineViewProps) {
  const groups = groupNodesForTimeline(nodes);

  return (
    <section className="panel day-timeline-view">
      <div className="panel-heading">
        <div>
          <h2>今日事件</h2>
          <p>{formatStardewDate(runtimeState)}</p>
        </div>
        <p>
          当前显示 {nodes.length} / 总计 {totalCount}
        </p>
      </div>

      <div className="timeline-groups">
        {groups.map((group) => (
          <section className="timeline-group" key={group.key}>
            <div className="timeline-group__header">
              <h3>{group.title}</h3>
              <span>{group.nodes.length}</span>
            </div>

            {group.nodes.length === 0 ? (
              <p className="empty-state">这个分组里暂无事件。</p>
            ) : (
              <div className="timeline-item-list">
                {group.nodes.map((node) => {
                  const timeWindow = extractTimeWindow(node);
                  const shortReason = buildShortReason(node);
                  const conflictCount = node.nodeId
                    ? (conflictCountByNodeId[node.nodeId] ?? 0)
                    : 0;
                  const characters = extractCharactersFromNode(node);

                  return (
                    <button
                      className={`timeline-item${
                        node.nodeId === selectedNodeId
                          ? " timeline-item--selected"
                          : ""
                      }`}
                      key={node.nodeId ?? `${node.eventId}-${node.sourceModId}`}
                      onClick={() => onSelectNode(node)}
                      type="button"
                      title={node.rawKey ?? undefined}
                    >
                      <div className="timeline-item__header">
                        <strong>
                          {timeWindow
                            ? formatTimeRangeZh(timeWindow.start, timeWindow.end)
                            : "任意时间"}
                        </strong>
                        <div className="timeline-item__tags">
                          <span>{node.eventId ?? "未知事件"}</span>
                          <span>{formatStatusLabel(node.status)}</span>
                          {conflictCount > 0 ? (
                            <span className="conflict-badge">
                              潜在冲突 x{conflictCount}
                            </span>
                          ) : null}
                        </div>
                      </div>
                      <div className="timeline-item__meta">
                        <span>{node.sourceModName ?? "未知 Mod"}</span>
                        <span title={node.location ?? ""}>
                          {formatLocationZh(node.location, node.sourceModId)}
                        </span>
                      </div>
                      {characters.length > 0 ? (
                        <div className="timeline-item__characters">
                          {characters.slice(0, 6).map((name) => (
                            <span
                              className="character-chip character-chip--inline"
                              key={`${node.nodeId}-${name}`}
                              title={name}
                            >
                              {translateCharacter(name, node.sourceModId).zh}
                            </span>
                          ))}
                          {characters.length > 6 ? (
                            <span className="character-chip character-chip--inline">
                              +{characters.length - 6}
                            </span>
                          ) : null}
                        </div>
                      ) : null}
                      <p className="timeline-item__reason">{shortReason}</p>
                      <div className="condition-summary" aria-label="事件摘要">
                        <span>地点：{formatLocationZh(node.location, node.sourceModId)}</span>
                        <span>已满足 {countPassedAtoms(node)}</span>
                        <span>未知 {countUnknownConditions(node)}</span>
                        {node.patchWhenConditions?.length ? (
                          <span>CP When：{node.patchWhenConditions.length}</span>
                        ) : null}
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </section>
        ))}
      </div>
    </section>
  );
}

function countPassedAtoms(node: StoryNodeEvaluation): number {
  return (node.conditionResult?.atomResults ?? []).filter(
    (atom) => atom.passed === true,
  ).length;
}

function countUnknownConditions(node: StoryNodeEvaluation): number {
  const unknownAtoms = (node.conditionResult?.atomResults ?? []).filter(
    (atom) => atom.passed == null,
  ).length;
  return (
    unknownAtoms +
    (node.unknownFragments?.length ?? 0) +
    (node.patchWhenConditions?.filter((condition) => !condition.isKnown).length ?? 0)
  );
}

function buildShortReason(node: StoryNodeEvaluation): string {
  const reason = formatStatusReasonZh(node.statusReason, node);
  return reason.length > 140 ? `${reason.slice(0, 137)}...` : reason;
}
