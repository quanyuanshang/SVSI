import { translateCharacter } from "../lib/translations";
import type { PotentialConflict } from "../lib/conflictDetection";
import type { StoryNodeEvaluation } from "../types/story";

interface ConflictPanelProps {
  conflicts: PotentialConflict[];
  nodesById: Map<string, StoryNodeEvaluation>;
  onSelectNodeId: (nodeId: string) => void;
}

export function ConflictPanel({
  conflicts,
  nodesById,
  onSelectNodeId,
}: ConflictPanelProps) {
  return (
    <section className="panel conflict-panel">
      <div className="panel-heading">
        <div>
          <h2>潜在冲突</h2>
          <p>检测到 {conflicts.length} 组可能重叠的事件</p>
        </div>
      </div>

      {conflicts.length === 0 ? (
        <p className="empty-state">当前没有发现潜在冲突。</p>
      ) : (
        <ul className="conflict-list">
          {conflicts.map((conflict) => {
            const nodeA = nodesById.get(conflict.nodeAId);
            const nodeB = nodesById.get(conflict.nodeBId);
            const sharedNpcNames = conflict.sharedNpcNames.map((name) => translateCharacter(name).zh);

            return (
              <li
                className={`conflict-item conflict-item--${conflict.severity}`}
                key={conflict.id}
              >
                <div className="conflict-item__header">
                  <strong>{sharedNpcNames.join("、")}</strong>
                  <span className="conflict-severity">
                    {conflict.severity === "warning" ? "警告" : "提示"}
                  </span>
                </div>

                <p className="conflict-item__mods">
                  {(nodeA?.sourceModName ?? "未知 Mod")} vs {(nodeB?.sourceModName ?? "未知 Mod")}
                </p>

                <p className="conflict-item__events">
                  事件 {(nodeA?.eventId ?? "未知")}（
                  <button
                    className="link-button"
                    onClick={() => onSelectNodeId(conflict.nodeAId)}
                    type="button"
                  >
                    {conflict.nodeAId}
                  </button>
                  ）
                  {" / "}
                  事件 {(nodeB?.eventId ?? "未知")}（
                  <button
                    className="link-button"
                    onClick={() => onSelectNodeId(conflict.nodeBId)}
                    type="button"
                  >
                    {conflict.nodeBId}
                  </button>
                  ）
                </p>

                <p className="conflict-item__reason">{conflict.reason}</p>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
