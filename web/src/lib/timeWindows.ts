import type { StoryNodeEvaluation, StoryNodeStatus } from "../types/story";

export interface StoryTimeWindow {
  start: number;
  end: number;
}

export interface TimelineGroup {
  key: StoryNodeStatus;
  title: string;
  nodes: StoryNodeEvaluation[];
}

const TIMELINE_ORDER: StoryNodeStatus[] = [
  "Current",
  "AvailableLater",
  "Locked",
  "Unknown",
  "Triggered",
];

const GROUP_TITLES: Record<StoryNodeStatus, string> = {
  Current: "当前可触发",
  AvailableLater: "可触发但当前上下文不满足",
  Locked: "锁定",
  Unknown: "无法判断",
  Triggered: "已触发",
};

export function extractTimeWindow(
  node: StoryNodeEvaluation,
): StoryTimeWindow | null {
  const atom = node.conditionResult?.atomResults?.find(
    (item) => item.atomType === "Time" && item.raw,
  );

  if (!atom?.raw) {
    return null;
  }

  const match = atom.raw.match(/^(?:t|Time)\s+(\d{3,4})\s+(\d{3,4})$/i);
  if (!match) {
    return null;
  }

  const start = Number.parseInt(match[1], 10);
  const end = Number.parseInt(match[2], 10);

  if (Number.isNaN(start) || Number.isNaN(end)) {
    return null;
  }

  return { start, end };
}

export function groupNodesForTimeline(
  nodes: StoryNodeEvaluation[],
): TimelineGroup[] {
  return TIMELINE_ORDER.map((status) => ({
    key: status,
    title: GROUP_TITLES[status],
    nodes: nodes
      .filter((node) => node.status === status)
      .sort(compareTimelineNodes),
  }));
}

function compareTimelineNodes(
  left: StoryNodeEvaluation,
  right: StoryNodeEvaluation,
): number {
  const leftWindow = extractTimeWindow(left);
  const rightWindow = extractTimeWindow(right);

  if (leftWindow && rightWindow) {
    if (leftWindow.start !== rightWindow.start) {
      return leftWindow.start - rightWindow.start;
    }

    if (leftWindow.end !== rightWindow.end) {
      return leftWindow.end - rightWindow.end;
    }
  } else if (leftWindow && !rightWindow) {
    return -1;
  } else if (!leftWindow && rightWindow) {
    return 1;
  }

  return `${left.sourceModName ?? ""}:${left.eventId ?? ""}`.localeCompare(
    `${right.sourceModName ?? ""}:${right.eventId ?? ""}`,
  );
}
