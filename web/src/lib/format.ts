import type { RuntimeGameState, StoryNodeEvaluation, StoryNodeStatus } from "../types/story";
import { formatSeasonZh, formatWeatherZh, translateLocation } from "./translations";

export function formatStardewDate(runtimeState?: RuntimeGameState | null): string {
  if (!runtimeState) {
    return "日期未知";
  }

  return `第 ${runtimeState.year} 年 / ${formatSeasonZh(runtimeState.season)} / ${runtimeState.dayOfMonth} 日 / ${runtimeState.dayOfWeek}`;
}

export function formatStardewTime(time?: number | null): string {
  if (typeof time !== "number" || Number.isNaN(time)) {
    return "--:--";
  }

  const hours = Math.floor(time / 100)
    .toString()
    .padStart(2, "0");
  const minutes = (time % 100).toString().padStart(2, "0");
  return `${hours}:${minutes}`;
}

export function formatTimeRangeZh(start?: number | null, end?: number | null): string {
  if (typeof start !== "number" || typeof end !== "number") {
    return "任意时间";
  }

  return `${formatStardewTime(start)}-${formatStardewTime(end)}`;
}

export function formatStatusLabel(status?: StoryNodeStatus | null): string {
  switch (status) {
    case "Triggered":
      return "已触发";
    case "Current":
      return "可触发";
    case "AvailableLater":
      return "暂不可触发";
    case "Locked":
      return "前置未满足";
    case "Unknown":
      return "条件未知";
    default:
      return "条件未知";
  }
}

export function statusSortRank(status?: StoryNodeStatus | null): number {
  switch (status) {
    case "Current":
      return 0;
    case "AvailableLater":
      return 1;
    case "Locked":
      return 2;
    case "Unknown":
      return 3;
    case "Triggered":
      return 4;
    default:
      return 5;
  }
}

export function formatLocationZh(location?: string | null, sourceMod?: string | null): string {
  return translateLocation(location, sourceMod).zh;
}

export function formatStatusReasonZh(
  reason?: string,
  node?: Pick<StoryNodeEvaluation, "eventId" | "location" | "sourceModId">,
): string {
  if (!reason) {
    return "暂无状态说明。";
  }

  const locationMatch = reason.match(
    /player is currently at (?<current>.+), event location is (?<required>.+)\./i,
  );
  if (locationMatch?.groups) {
    return `地点不满足：需要在「${formatLocationZh(locationMatch.groups.required, node?.sourceModId)}」，当前在「${formatLocationZh(locationMatch.groups.current, node?.sourceModId)}」`;
  }

  if (reason.startsWith("Event ") && reason.endsWith(" has already been seen.")) {
    return `事件已触发：事件 ${node?.eventId ?? "未知"} 已在存档中记录为看过。`;
  }

  if (reason.startsWith("Progression conditions failed:")) {
    return `前置条件未满足：${reason.replace("Progression conditions failed:", "").trim()}`;
  }

  if (reason.startsWith("Patch-level progression conditions failed:")) {
    return `CP 前置条件未满足：${reason.replace("Patch-level progression conditions failed:", "").trim()}`;
  }

  if (reason.startsWith("Context conditions not currently met:")) {
    return `当前上下文不满足：${reason.replace("Context conditions not currently met:", "").trim()}`;
  }

  if (reason.startsWith("Patch-level When conditions are not evaluated:")) {
    return "CP When 条件暂未参与静态评估，因此当前状态被标记为“条件未知”。";
  }

  if (reason.includes("Unknown fragments:") || reason.includes("Unknown atoms:")) {
    return "存在未解析条件，已保留原始数据，当前无法安全判断是否可触发。";
  }

  if (reason.includes("non-numeric and has no preconditions")) {
    return "事件 ID 不是普通地点触发事件格式，且没有前置条件，当前按“条件未知”处理。";
  }

  if (reason === "All known conditions are satisfied and player is at the event location.") {
    return "所有已知条件均已满足，且玩家当前就在事件触发地点。";
  }

  return `调试原因：${reason}`;
}

export { formatSeasonZh, formatWeatherZh };
