import { formatStatusLabel } from "../lib/format";
import type { ConditionAtomResult, StoryNodeEvaluation } from "../types/story";

interface StoryNodeDetailProps {
  node: StoryNodeEvaluation | null;
}

function renderAtomValue(atom: ConditionAtomResult): string {
  if (atom.passed === true) {
    return "Passed";
  }

  if (atom.passed === false) {
    return "Failed";
  }

  return "Unknown";
}

export function StoryNodeDetail({ node }: StoryNodeDetailProps) {
  if (!node) {
    return (
      <section className="panel story-node-detail story-node-detail--empty">
        <p className="empty-state">Select a story node</p>
      </section>
    );
  }

  const atomResults = node.conditionResult?.atomResults ?? [];

  return (
    <section className="panel story-node-detail">
      <div className="panel-heading">
        <h2>Story Node Detail</h2>
        <p>{node.nodeId ?? "Unknown node"}</p>
      </div>

      <dl className="detail-grid">
        <div>
          <dt>Event ID</dt>
          <dd>{node.eventId ?? "Unknown"}</dd>
        </div>
        <div>
          <dt>Source Mod</dt>
          <dd>{node.sourceModName ?? "Unknown"}</dd>
        </div>
        <div>
          <dt>Location</dt>
          <dd>{node.location ?? "Unknown"}</dd>
        </div>
        <div>
          <dt>Status</dt>
          <dd>{formatStatusLabel(node.status)}</dd>
        </div>
      </dl>

      <div className="detail-block">
        <h3>Status Reason</h3>
        <p>{node.statusReason ?? "No status reason."}</p>
      </div>

      <div className="detail-block">
        <h3>Condition Atom Results</h3>
        {atomResults.length === 0 ? (
          <p className="empty-state">No atom results available.</p>
        ) : (
          <ul className="atom-result-list">
            {atomResults.map((atom, index) => (
              <li
                className="atom-result-card"
                key={`${atom.raw ?? atom.atomType}-${index}`}
              >
                <div className="atom-result-card__header">
                  <strong>{atom.atomType ?? "Unknown"}</strong>
                  <span>{renderAtomValue(atom)}</span>
                </div>
                <p className="atom-result-card__raw">{atom.raw ?? "No raw fragment"}</p>
                <p className="atom-result-card__reason">
                  {atom.reason ?? "No reason provided."}
                </p>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
