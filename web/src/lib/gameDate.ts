import type { GameDateSnapshot } from "../types/history";
import { formatSeasonZh, formatStardewTime } from "./format";

const seasonRanks: Record<string, number> = {
  spring: 0,
  summer: 1,
  fall: 2,
  winter: 3,
};

export function seasonSortRank(season?: string | null): number {
  if (!season) {
    return Number.MAX_SAFE_INTEGER;
  }

  return seasonRanks[season.toLowerCase()] ?? Number.MAX_SAFE_INTEGER;
}

export function compareGameDates(
  left?: GameDateSnapshot,
  right?: GameDateSnapshot,
): number {
  const leftDate = left ?? emptyDate();
  const rightDate = right ?? emptyDate();

  return (
    leftDate.year - rightDate.year ||
    seasonSortRank(leftDate.season) - seasonSortRank(rightDate.season) ||
    leftDate.dayOfMonth - rightDate.dayOfMonth ||
    leftDate.time - rightDate.time
  );
}

export function formatGameDate(date?: GameDateSnapshot): string {
  if (!date) {
    return "日期未知";
  }

  return `第 ${date.year} 年 / ${formatSeasonZh(date.season)} / ${date.dayOfMonth} 日 / ${formatStardewTime(date.time)}`;
}

function emptyDate(): GameDateSnapshot {
  return {
    year: Number.MAX_SAFE_INTEGER,
    season: "",
    dayOfMonth: Number.MAX_SAFE_INTEGER,
    time: Number.MAX_SAFE_INTEGER,
  };
}
