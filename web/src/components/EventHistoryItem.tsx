import { formatLocationZh } from "../lib/format";
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
      title={entry.location || undefined}
    >
      <div className="timeline-item__header">
        <strong>{entry.eventId || "未知事件"}</strong>
        <div className="timeline-item__tags">
          <span>{entry.observationSource || "事件历史"}</span>
          {entry.nodeId ? <span>已匹配事件节点</span> : null}
        </div>
      </div>
      <div className="timeline-item__meta">
        <span>{entry.sourceModName || entry.sourceModId || "已匹配故事事件"}</span>
        <span>{formatLocationZh(entry.location)}</span>
      </div>
      <p className="timeline-item__reason">
        首次记录：{formatGameDate(entry.firstSeenGameDate ?? entry.date)}
      </p>
    </button>
  );
}
