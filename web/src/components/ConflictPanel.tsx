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
          <h2>Potential conflict</h2>
          <p>Detected {conflicts.length} possible overlaps</p>
        </div>
      </div>

      {conflicts.length === 0 ? (
        <p className="empty-state">No Potential conflict found.</p>
      ) : (
        <ul className="conflict-list">
          {conflicts.map((conflict) => {
            const nodeA = nodesById.get(conflict.nodeAId);
            const nodeB = nodesById.get(conflict.nodeBId);

            return (
              <li
                className={`conflict-item conflict-item--${conflict.severity}`}
                key={conflict.id}
              >
                <div className="conflict-item__header">
                  <strong>{conflict.sharedNpcNames.join(", ")}</strong>
                  <span className="conflict-severity">
                    {conflict.severity === "warning"
                      ? "Potential conflict (warning)"
                      : "Potential conflict"}
                  </span>
                </div>

                <p className="conflict-item__mods">
                  {(nodeA?.sourceModName ?? "Unknown mod")} vs {(nodeB?.sourceModName ?? "Unknown mod")}
                </p>

                <p className="conflict-item__events">
                  Event {(nodeA?.eventId ?? "Unknown")} (
                  <button
                    className="link-button"
                    onClick={() => onSelectNodeId(conflict.nodeAId)}
                    type="button"
                  >
                    {conflict.nodeAId}
                  </button>
                  )
                  {" "}
                  / Event {(nodeB?.eventId ?? "Unknown")} (
                  <button
                    className="link-button"
                    onClick={() => onSelectNodeId(conflict.nodeBId)}
                    type="button"
                  >
                    {conflict.nodeBId}
                  </button>
                  )
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