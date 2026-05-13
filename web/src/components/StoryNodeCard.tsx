import { formatStatusLabel } from "../lib/format";
import type { StoryNodeEvaluation } from "../types/story";

interface StoryNodeCardProps {
  node: StoryNodeEvaluation;
  selected: boolean;
  conflictCount?: number;
  onSelect: (node: StoryNodeEvaluation) => void;
}

export function StoryNodeCard({
  node,
  selected,
  conflictCount = 0,
  onSelect,
}: StoryNodeCardProps) {
  return (
    <button
      className={`story-card${selected ? " story-card--selected" : ""}`}
      onClick={() => onSelect(node)}
      type="button"
    >
      <div className="story-card__header">
        <div className="story-card__event-wrap">
          <span className="story-card__event-id">{node.eventId ?? "Unknown"}</span>
          {conflictCount > 0 ? (
            <span className="conflict-badge">Potential conflict x{conflictCount}</span>
          ) : null}
        </div>
        <span className={`status-chip status-chip--${node.status ?? "Unknown"}`}>
          {formatStatusLabel(node.status)}
        </span>
      </div>
      <div className="story-card__meta">
        <strong>{node.sourceModName ?? "Unknown mod"}</strong>
        <span>{node.location ?? "Unknown location"}</span>
      </div>
      <p className="story-card__reason">{node.statusReason ?? "No status reason."}</p>
    </button>
  );
}
