import type { RuntimeGameState, StoryNodeStatus } from "../types/story";

export function formatStardewDate(
  runtimeState?: RuntimeGameState | null,
): string {
  if (!runtimeState) {
    return "Unknown date";
  }

  return `Year ${runtimeState.year} / ${runtimeState.season} / Day ${runtimeState.dayOfMonth} / ${runtimeState.dayOfWeek}`;
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

export function formatStatusLabel(status?: StoryNodeStatus | null): string {
  switch (status) {
    case "Triggered":
      return "Triggered";
    case "Current":
      return "Current";
    case "AvailableLater":
      return "Available Later";
    case "Locked":
      return "Locked";
    case "Unknown":
      return "Unknown";
    default:
      return "Unknown";
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
