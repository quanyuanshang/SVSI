import { compareGameDates } from "./gameDate";
import { extractCharactersFromNode } from "./characters";
import { extractTimeWindow, type StoryTimeWindow } from "./timeWindows";
import type { ObservedEventHistoryEntry } from "../types/history";
import type {
  PatchWhenCondition,
  RuntimeGameState,
  StoryNodeEvaluation,
  StoryNodeStatus,
} from "../types/story";

export interface StoryGraphReference {
  eventId: string;
  node: StoryEventNode | null;
}

export interface StoryEventNode {
  key: string;
  id?: string;
  eventId?: string;
  rawName?: string;
  displayName: string;
  modName?: string;
  characters: string[];
  location?: string;
  timeWindow: StoryTimeWindow | null;
  season?: string;
  weather?: string;
  status?: StoryNodeStatus;
  statusReason?: string;
  prerequisites: StoryGraphReference[];
  dependents: StoryGraphReference[];
  conflicts: number;
  resolvedConditions: string[];
  unresolvedConditions: string[];
  unmetConditions: string[];
  isBlocked: boolean;
  blockReason?: string;
  isSeen: boolean;
  isTriggerableToday: boolean;
  lastTriggeredAt?: ObservedEventHistoryEntry;
  source: StoryNodeEvaluation;
}

export interface StoryGraph {
  nodes: StoryEventNode[];
  nodesByKey: Map<string, StoryEventNode>;
  nodesByEventId: Map<string, StoryEventNode[]>;
}

export interface StorylineSections {
  triggered: StoryEventNode[];
  latestTriggered: StoryEventNode | null;
  current: StoryEventNode[];
  locked: StoryEventNode[];
  downstream: StoryEventNode[];
  warnings: StoryEventNode[];
}

export interface EventDependencySections {
  upstream: StoryGraphReference[];
  focus: StoryEventNode | null;
  downstream: StoryGraphReference[];
  warnings: string[];
}

export interface TodayActionGroups {
  ready: StoryEventNode[];
  later: StoryEventNode[];
  locked: StoryEventNode[];
  conflicts: StoryEventNode[];
  incomplete: StoryEventNode[];
}

const EVENT_SEEN_ATOM_TYPES = new Set([
  "EventSeen",
  "SeenEvent",
  "HostSeenEvent",
  "HasSeenEvent",
]);

export function buildStoryGraph(
  nodes: StoryNodeEvaluation[],
  historyEntries: ObservedEventHistoryEntry[] = [],
  conflictCountByNodeId: Record<string, number> = {},
  runtimeState?: RuntimeGameState | null,
): StoryGraph {
  const historyByNodeKey = buildHistoryByNodeKey(historyEntries);
  const storyNodes: StoryEventNode[] = nodes.map((node, index) => {
    const key = getNodeKey(node, index);
    const lastTriggeredAt = historyByNodeKey.get(key) ?? findHistoryByEventId(historyEntries, node.eventId);
    const playerKilledBlock = evaluatePlayerKilledBlock(node, runtimeState);
    const unmetConditions = collectUnmetConditions(node);
    const resolvedConditions = collectResolvedConditions(node);

    if (playerKilledBlock.isBlocked) {
      unmetConditions.unshift("需要前置：PlayerDied");
    } else if (playerKilledBlock.applies) {
      resolvedConditions.unshift("前置事件：PlayerDied 已发生");
    }

    return {
      key,
      id: node.nodeId,
      eventId: node.eventId,
      rawName: node.rawKey,
      displayName: node.eventId ?? node.rawKey ?? "Unknown event",
      modName: node.sourceModName,
      characters: extractCharactersFromNode(node),
      location: node.location,
      timeWindow: extractTimeWindow(node),
      season: extractFirstConditionValue(node, /^(?:s|z|Season)\b/i),
      weather: extractFirstConditionValue(node, /^!?w\b/i),
      status: playerKilledBlock.isBlocked ? "Locked" : node.status,
      statusReason: node.statusReason,
      prerequisites: [],
      dependents: [],
      conflicts: node.nodeId ? (conflictCountByNodeId[node.nodeId] ?? 0) : 0,
      resolvedConditions,
      unresolvedConditions: collectUnresolvedConditions(node),
      unmetConditions,
      isBlocked: playerKilledBlock.isBlocked,
      blockReason: playerKilledBlock.reason,
      isSeen: node.status === "Triggered" || Boolean(lastTriggeredAt),
      isTriggerableToday: node.status === "Current" && !playerKilledBlock.isBlocked,
      lastTriggeredAt,
      source: node,
    };
  });

  const nodesByKey = new Map(storyNodes.map((node) => [node.key, node]));
  const nodesByEventId = buildNodesByEventId(storyNodes);

  for (const storyNode of storyNodes) {
    const prerequisiteIds = extractPrerequisiteEventIds(storyNode.source);
    storyNode.prerequisites = prerequisiteIds.map((eventId) => ({
      eventId,
      node: findBestNodeForEventId(nodesByEventId, eventId),
    }));

    for (const prerequisite of storyNode.prerequisites) {
      prerequisite.node?.dependents.push({
        eventId: storyNode.eventId ?? storyNode.key,
        node: storyNode,
      });
    }
  }

  return {
    nodes: storyNodes,
    nodesByKey,
    nodesByEventId,
  };
}

export function buildStorylineSections(
  graph: StoryGraph,
  scopedNodes: StoryEventNode[],
): StorylineSections {
  const scopedKeys = new Set(scopedNodes.map((node) => node.key));
  const downstream = new Map<string, StoryEventNode>();

  for (const node of scopedNodes) {
    for (const dependent of node.dependents) {
      if (dependent.node && !scopedKeys.has(dependent.node.key)) {
        downstream.set(dependent.node.key, dependent.node);
      }
    }
  }

  const triggered = sortStoryNodes(
    scopedNodes.filter((node) => node.status === "Triggered"),
  );

  return {
    triggered,
    latestTriggered: findLatestTriggered(triggered),
    current: sortStoryNodes(scopedNodes.filter((node) => node.status === "Current")),
    locked: sortStoryNodes(
      scopedNodes.filter((node) => node.status === "Locked" || node.status === "AvailableLater"),
    ),
    downstream: sortStoryNodes(Array.from(downstream.values())),
    warnings: sortStoryNodes(
      scopedNodes.filter(
        (node) =>
          node.status === "Unknown" ||
          node.unresolvedConditions.length > 0 ||
          node.prerequisites.some((item) => !item.node),
      ),
    ),
  };
}

export function buildEventDependencySections(
  graph: StoryGraph,
  eventId: string,
): EventDependencySections {
  const focus = findBestNodeForEventId(graph.nodesByEventId, eventId) ?? null;

  if (!focus) {
    return {
      upstream: [{ eventId, node: null }],
      focus: null,
      downstream: [],
      warnings: [`No indexed event node matched "${eventId}".`],
    };
  }

  return {
    upstream: focus.prerequisites,
    focus,
    downstream: focus.dependents,
    warnings: [
      ...focus.unresolvedConditions,
      ...focus.prerequisites
        .filter((item) => !item.node)
        .map((item) => `Unresolved prerequisite event ${item.eventId}`),
    ],
  };
}

export function buildTodayActionGroups(
  nodes: StoryEventNode[],
  conflictCountByNodeId: Record<string, number> = {},
): TodayActionGroups {
  const withConflicts = nodes.map((node) => ({
    ...node,
    conflicts: node.id ? (conflictCountByNodeId[node.id] ?? node.conflicts) : node.conflicts,
  }));

  return {
    ready: sortByUnlockImpact(withConflicts.filter((node) => node.status === "Current")),
    later: sortByUnlockImpact(withConflicts.filter((node) => node.status === "AvailableLater")),
    locked: sortByUnlockImpact(withConflicts.filter((node) => node.status === "Locked")),
    conflicts: sortByUnlockImpact(withConflicts.filter((node) => node.conflicts > 0)),
    incomplete: sortByUnlockImpact(
      withConflicts.filter(
        (node) => node.status === "Unknown" || node.unresolvedConditions.length > 0,
      ),
    ),
  };
}

export function findStoryNodeBySource(
  graph: StoryGraph,
  source: StoryNodeEvaluation | null,
): StoryEventNode | null {
  if (!source) {
    return null;
  }

  if (source.nodeId) {
    return graph.nodesByKey.get(source.nodeId) ?? null;
  }

  if (source.eventId) {
    return findBestNodeForEventId(graph.nodesByEventId, source.eventId) ?? null;
  }

  return null;
}

function getNodeKey(node: StoryNodeEvaluation, index: number): string {
  return node.nodeId ?? `${node.sourceModId ?? "mod"}:${node.location ?? "location"}:${node.eventId ?? index}`;
}

function buildNodesByEventId(nodes: StoryEventNode[]): Map<string, StoryEventNode[]> {
  const map = new Map<string, StoryEventNode[]>();

  for (const node of nodes) {
    if (!node.eventId) {
      continue;
    }

    map.set(node.eventId, [...(map.get(node.eventId) ?? []), node]);
  }

  return map;
}

function findBestNodeForEventId(
  nodesByEventId: Map<string, StoryEventNode[]>,
  eventId: string,
): StoryEventNode | null {
  const matches = nodesByEventId.get(eventId);
  if (!matches?.length) {
    return null;
  }

  return matches[0];
}

function buildHistoryByNodeKey(
  entries: ObservedEventHistoryEntry[],
): Map<string, ObservedEventHistoryEntry> {
  const map = new Map<string, ObservedEventHistoryEntry>();

  for (const entry of entries) {
    if (!entry.nodeId) {
      continue;
    }

    const existing = map.get(entry.nodeId);
    if (!existing || compareHistoryEntries(existing, entry) < 0) {
      map.set(entry.nodeId, entry);
    }
  }

  return map;
}

function findHistoryByEventId(
  entries: ObservedEventHistoryEntry[],
  eventId?: string,
): ObservedEventHistoryEntry | undefined {
  if (!eventId) {
    return undefined;
  }

  return entries
    .filter((entry) => entry.eventId === eventId)
    .sort(compareHistoryEntries)
    .at(-1);
}

function compareHistoryEntries(
  left: ObservedEventHistoryEntry,
  right: ObservedEventHistoryEntry,
): number {
  return compareGameDates(
    left.firstSeenGameDate ?? left.date,
    right.firstSeenGameDate ?? right.date,
  );
}

function extractPrerequisiteEventIds(node: StoryNodeEvaluation): string[] {
  const ids = new Set<string>();

  if (isPlayerKilledNode(node)) {
    ids.add("PlayerDied");
  }

  for (const raw of node.rawPreconditions ?? []) {
    collectEventIdsFromCondition(raw, ids);
  }

  for (const atom of node.conditionResult?.atomResults ?? []) {
    if (atom.raw && (!atom.atomType || EVENT_SEEN_ATOM_TYPES.has(atom.atomType))) {
      collectEventIdsFromCondition(atom.raw, ids);
    }
  }

  return Array.from(ids).sort((a, b) => a.localeCompare(b));
}

function evaluatePlayerKilledBlock(
  node: StoryNodeEvaluation,
  runtimeState?: RuntimeGameState | null,
): { applies: boolean; isBlocked: boolean; reason?: string } {
  if (!isPlayerKilledNode(node)) {
    return { applies: false, isBlocked: false };
  }

  const seen = new Set(runtimeState?.seenEvents ?? []);
  if (seen.has("PlayerDied")) {
    return { applies: true, isBlocked: false };
  }

  return {
    applies: true,
    isBlocked: true,
    reason: "当前事件已被阻止，因为前置事件 PlayerDied 尚未发生。",
  };
}

function isPlayerKilledNode(node: StoryNodeEvaluation): boolean {
  const candidates = [
    node.eventId,
    node.rawKey,
    node.rawScriptPreview,
    node.statusReason,
  ]
    .filter((value): value is string => Boolean(value))
    .map((value) => value.toLowerCase());

  return candidates.some((value) => /\bplayerkilled\b/i.test(value));
}

function collectEventIdsFromCondition(raw: string, sink: Set<string>): void {
  const trimmed = raw.trim();
  if (!trimmed) {
    return;
  }

  const patterns = [
    /^(?:e|E|eventSeen|EventSeen|HasSeenEvent|HostSeenEvent)\s+([^\s/]+)/,
    /\b(?:eventSeen|EventSeen|HasSeenEvent|HostSeenEvent)\s+([^\s/]+)/g,
  ];

  for (const pattern of patterns) {
    let match: RegExpExecArray | null;
    while ((match = pattern.exec(trimmed)) !== null) {
      const eventId = cleanEventId(match[1]);
      if (eventId) {
        sink.add(eventId);
      }

      if (!pattern.global) {
        break;
      }
    }
  }
}

function cleanEventId(value?: string): string | null {
  const cleaned = value?.trim().replace(/^["']|["']$/g, "");
  return cleaned || null;
}

function collectUnresolvedConditions(node: StoryNodeEvaluation): string[] {
  const unresolved = new Set<string>();

  for (const fragment of node.unknownFragments ?? []) {
    if (fragment.trim()) {
      unresolved.add(fragment.trim());
    }
  }

  for (const atom of node.conditionResult?.atomResults ?? []) {
    if (atom.passed == null && atom.raw?.trim()) {
      unresolved.add(atom.raw.trim());
    }
  }

  for (const condition of node.patchWhenConditions ?? []) {
    if (!condition.isKnown) {
      unresolved.add(formatPatchWhenCondition(condition));
    }
  }

  return Array.from(unresolved);
}

function collectUnmetConditions(node: StoryNodeEvaluation): string[] {
  const unmet = new Set<string>();

  for (const atom of node.conditionResult?.atomResults ?? []) {
    if (atom.passed === false && atom.raw?.trim()) {
      unmet.add(atom.raw.trim());
    }
  }

  for (const condition of node.patchWhenConditions ?? []) {
    if (condition.passed === false) {
      unmet.add(formatPatchWhenCondition(condition));
    }
  }

  return Array.from(unmet);
}

function collectResolvedConditions(node: StoryNodeEvaluation): string[] {
  const resolved = new Set<string>();

  for (const atom of node.conditionResult?.atomResults ?? []) {
    if (atom.passed === true && atom.raw?.trim()) {
      resolved.add(atom.raw.trim());
    }
  }

  for (const condition of node.patchWhenConditions ?? []) {
    if (condition.passed === true) {
      resolved.add(formatPatchWhenCondition(condition));
    }
  }

  return Array.from(resolved);
}

function formatPatchWhenCondition(condition: PatchWhenCondition): string {
  const key = condition.key ?? "When";
  const value = condition.value ?? condition.rawValue ?? condition.reasonZh ?? condition.reason ?? "";
  return value ? `${key}: ${value}` : key;
}

function extractFirstConditionValue(
  node: StoryNodeEvaluation,
  pattern: RegExp,
): string | undefined {
  const raw = node.rawPreconditions?.find((condition) => pattern.test(condition));
  return raw?.split(/\s+/).slice(1).join(" ") || undefined;
}

function findLatestTriggered(nodes: StoryEventNode[]): StoryEventNode | null {
  if (nodes.length === 0) {
    return null;
  }

  return [...nodes].sort((left, right) => {
    if (left.lastTriggeredAt && right.lastTriggeredAt) {
      return compareHistoryEntries(left.lastTriggeredAt, right.lastTriggeredAt);
    }

    if (left.lastTriggeredAt && !right.lastTriggeredAt) {
      return 1;
    }

    if (!left.lastTriggeredAt && right.lastTriggeredAt) {
      return -1;
    }

    return compareStoryNodes(left, right);
  }).at(-1) ?? null;
}

function sortStoryNodes(nodes: StoryEventNode[]): StoryEventNode[] {
  return [...nodes].sort(compareStoryNodes);
}

function sortByUnlockImpact(nodes: StoryEventNode[]): StoryEventNode[] {
  return [...nodes].sort((left, right) => {
    const impact = right.dependents.length - left.dependents.length;
    return impact || compareStoryNodes(left, right);
  });
}

function compareStoryNodes(left: StoryEventNode, right: StoryEventNode): number {
  return (
    statusRank(left.status) - statusRank(right.status) ||
    (left.timeWindow?.start ?? Number.MAX_SAFE_INTEGER) -
      (right.timeWindow?.start ?? Number.MAX_SAFE_INTEGER) ||
    (left.modName ?? "").localeCompare(right.modName ?? "") ||
    (left.eventId ?? left.key).localeCompare(right.eventId ?? right.key)
  );
}

function statusRank(status?: StoryNodeStatus): number {
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
