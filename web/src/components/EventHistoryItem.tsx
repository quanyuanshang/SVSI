import { formatGameDate } from "../lib/gameDate";
import type { ObservedEventHistoryEntry } from "../types/history";

interface EventHistoryItemProps {
  entry: ObservedEventHistoryEntry;
  selected: boolean;
  onSelect: (entry: ObservedEventHistoryEntry) => void;
}

export function EventHistoryItem({
  entry,
  selected,
  onSelect,
}: EventHistoryItemProps) {
  return (
    <button
      className={`timeline-item${selected ? " timeline-item--selected" : ""}`}
      disabled={!entry.nodeId}
      onClick={() => onSelect(entry)}
      type="button"
    >
      <div className="timeline-item__header">
        <strong>{entry.eventId || "Unknown event"}</strong>
        <div className="timeline-item__tags">
          <span>{entry.observationSource || "event history"}</span>
          {entry.nodeId ? <span>matched node</span> : null}
        </div>
      </div>
      <div className="timeline-item__meta">
        <span>{entry.sourceModName || entry.sourceModId || "Matched story event"}</span>
        <span>{entry.location || "Unknown location"}</span>
      </div>
      <p className="timeline-item__reason">
        First seen: {formatGameDate(entry.firstSeenGameDate ?? entry.date)}
      </p>
    </button>
  );
}
