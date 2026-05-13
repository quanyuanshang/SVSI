import { formatStatusLabel } from "../lib/format";
import { formatGameDate } from "../lib/gameDate";
import type { ObservedEventHistoryEntry } from "../types/history";
import type { ConditionAtomResult, StoryNodeEvaluation } from "../types/story";

interface StoryNodeDetailProps {
  node: StoryNodeEvaluation | null;
  historyEntries?: ObservedEventHistoryEntry[];
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

export function StoryNodeDetail({
  node,
  historyEntries = [],
}: StoryNodeDetailProps) {
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
        <h3>Trigger Conditions</h3>
        <div className="condition-summary condition-summary--stacked">
          <span>Raw key: {node.rawKey ?? node.eventId ?? "Unknown"}</span>
          <span>
            Raw preconditions:{" "}
            {node.rawPreconditions?.length
              ? node.rawPreconditions.join(" / ")
              : "none"}
          </span>
          <span>
            Unknown fragments:{" "}
            {node.unknownFragments?.length
              ? node.unknownFragments.join(" / ")
              : "none"}
          </span>
        </div>
      </div>

      <div className="detail-block">
        <h3>CP When Conditions</h3>
        {node.patchWhenConditions?.length ? (
          <ul className="atom-result-list">
            {node.patchWhenConditions.map((condition, index) => (
              <li
                className="atom-result-card"
                key={`${condition.key ?? "when"}-${index}`}
              >
                <div className="atom-result-card__header">
                  <strong>{condition.key ?? "Unknown When"}</strong>
                  <span>{condition.isKnown ? "Known" : "Unknown"}</span>
                </div>
                <p className="atom-result-card__raw">
                  {condition.value ?? condition.rawValue ?? "No value"}
                </p>
                <p className="atom-result-card__reason">
                  {condition.reason ?? "Patch-level condition was not evaluated."}
                </p>
              </li>
            ))}
          </ul>
        ) : (
          <p className="empty-state">No patch-level When conditions.</p>
        )}
      </div>

      <div className="detail-block">
        <h3>Local History</h3>
        {historyEntries.length === 0 ? (
          <p className="empty-state">No local event history for this node.</p>
        ) : (
          <ul className="atom-result-list">
            {historyEntries.map((entry) => (
              <li
                className="atom-result-card"
                key={`${entry.eventId}-${entry.observedAtUtc ?? ""}`}
              >
                <div className="atom-result-card__header">
                  <strong>{entry.eventId || "Unknown event"}</strong>
                  <span>{entry.observationSource || "event history"}</span>
                </div>
                <p>
                  First seen:{" "}
                  {formatGameDate(entry.firstSeenGameDate ?? entry.date)}
                </p>
                <p className="atom-result-card__reason">
                  {entry.location || "Unknown location"}
                </p>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="detail-block">
        <h3>Known Condition Atom Results</h3>
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
