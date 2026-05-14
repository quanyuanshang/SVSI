import { EventHistoryItem } from "./EventHistoryItem";
import { compareGameDates, formatGameDate, seasonSortRank } from "../lib/gameDate";
import { formatSeasonZh } from "../lib/format";
import type { ObservedEventHistoryEntry } from "../types/history";

interface ProgressTimelineViewProps {
  entries: ObservedEventHistoryEntry[];
  loading: boolean;
  error: string | null;
  selectedNodeId: string | null;
  onSelectEntry: (entry: ObservedEventHistoryEntry) => void;
}

interface DayGroup {
  year: number;
  season: string;
  dayOfMonth: number;
  entries: ObservedEventHistoryEntry[];
}

interface SeasonGroup {
  season: string;
  days: DayGroup[];
}

interface YearGroup {
  year: number;
  seasons: SeasonGroup[];
}

export function ProgressTimelineView({
  entries,
  loading,
  error,
  selectedNodeId,
  onSelectEntry,
}: ProgressTimelineViewProps) {
  const groups = groupHistoryEntries(entries);

  return (
    <section className="panel day-timeline-view">
      <div className="panel-heading">
        <div>
          <h2>事件历史</h2>
          <p>共记录 {entries.length} 条事件</p>
        </div>
        {loading ? <p>历史加载中...</p> : null}
      </div>

      {error ? <p className="empty-state">{error}</p> : null}
      {!error && entries.length === 0 ? (
        <p className="empty-state">暂时还没有记录到已匹配的事件历史。</p>
      ) : null}

      <div className="timeline-groups">
        {groups.map((yearGroup) => (
          <section className="timeline-group" key={yearGroup.year}>
            <div className="timeline-group__header">
              <h3>第 {yearGroup.year} 年</h3>
              <span>{countYearEntries(yearGroup)}</span>
            </div>

            {yearGroup.seasons.map((seasonGroup) => (
              <section className="timeline-group" key={`${yearGroup.year}-${seasonGroup.season}`}>
                <div className="timeline-group__header">
                  <h3>{formatSeasonZh(seasonGroup.season)}</h3>
                  <span>{countSeasonEntries(seasonGroup)}</span>
                </div>

                {seasonGroup.days.map((dayGroup) => (
                  <section
                    className="timeline-group"
                    key={`${yearGroup.year}-${seasonGroup.season}-${dayGroup.dayOfMonth}`}
                  >
                    <div className="timeline-group__header">
                      <h3>{formatGameDate({ year: dayGroup.year, season: dayGroup.season, dayOfMonth: dayGroup.dayOfMonth, time: 600 })}</h3>
                      <span>{dayGroup.entries.length}</span>
                    </div>
                    <div className="timeline-item-list">
                      {dayGroup.entries.map((entry) => (
                        <EventHistoryItem
                          entry={entry}
                          key={`${entry.eventId}-${entry.observedAtUtc ?? ""}`}
                          selected={Boolean(entry.nodeId && entry.nodeId === selectedNodeId)}
                          onSelect={onSelectEntry}
                        />
                      ))}
                    </div>
                  </section>
                ))}
              </section>
            ))}
          </section>
        ))}
      </div>
    </section>
  );
}

function groupHistoryEntries(entries: ObservedEventHistoryEntry[]): YearGroup[] {
  const sortedEntries = [...entries].sort((left, right) =>
    compareGameDates(left.firstSeenGameDate ?? left.date, right.firstSeenGameDate ?? right.date),
  );
  const years = new Map<number, Map<string, Map<number, ObservedEventHistoryEntry[]>>>();

  for (const entry of sortedEntries) {
    const date = entry.firstSeenGameDate ?? entry.date;
    const year = date?.year ?? 0;
    const season = date?.season ?? "";
    const day = date?.dayOfMonth ?? 0;

    if (!years.has(year)) {
      years.set(year, new Map());
    }

    const seasons = years.get(year)!;
    if (!seasons.has(season)) {
      seasons.set(season, new Map());
    }

    const days = seasons.get(season)!;
    days.set(day, [...(days.get(day) ?? []), entry]);
  }

  return [...years.entries()].map(([year, seasons]) => ({
    year,
    seasons: [...seasons.entries()]
      .sort(([left], [right]) => seasonSortRank(left) - seasonSortRank(right))
      .map(([season, days]) => ({
        season,
        days: [...days.entries()].map(([dayOfMonth, dayEntries]) => ({
          year,
          season,
          dayOfMonth,
          entries: dayEntries,
        })),
      })),
  }));
}

function countYearEntries(group: YearGroup): number {
  return group.seasons.reduce(
    (total, season) => total + countSeasonEntries(season),
    0,
  );
}

function countSeasonEntries(group: SeasonGroup): number {
  return group.days.reduce((total, day) => total + day.entries.length, 0);
}
