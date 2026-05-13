import { formatStardewDate, formatStardewTime } from "../lib/format";
import { extractTimeWindow, groupNodesForTimeline } from "../lib/timeWindows";
import type {
  RuntimeGameState,
  StoryNodeEvaluation,
} from "../types/story";

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
          <h2>Day Timeline</h2>
          <p>{formatStardewDate(runtimeState)}</p>
        </div>
        <p>
          showing {nodes.length} / total {totalCount}
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
              <p className="empty-state">No nodes in this section.</p>
            ) : (
              <div className="timeline-item-list">
                {group.nodes.map((node) => {
                  const timeWindow = extractTimeWindow(node);
                  const shortReason = buildShortReason(node.statusReason);
                  const conflictCount = node.nodeId
                    ? (conflictCountByNodeId[node.nodeId] ?? 0)
                    : 0;

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
                    >
                      <div className="timeline-item__header">
                        <strong>
                          {timeWindow
                            ? `${formatStardewTime(timeWindow.start)}-${formatStardewTime(timeWindow.end)}`
                            : "Any time"}
                        </strong>
                        <div className="timeline-item__tags">
                          <span>{node.eventId ?? "Unknown"}</span>
                          {conflictCount > 0 ? (
                            <span className="conflict-badge">
                              Potential conflict x{conflictCount}
                            </span>
                          ) : null}
                        </div>
                      </div>
                      <div className="timeline-item__meta">
                        <span>{node.sourceModName ?? "Unknown mod"}</span>
                        <span>{node.location ?? "Unknown location"}</span>
                      </div>
                      <p className="timeline-item__reason">{shortReason}</p>
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

function buildShortReason(reason?: string): string {
  if (!reason) {
    return "No status reason.";
  }

  return reason.length > 140 ? `${reason.slice(0, 137)}...` : reason;
}
