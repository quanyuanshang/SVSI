import type { ObservedEventHistoryEntry } from "../types/history";
import type { StoryNodeEvaluation } from "../types/story";

export function findHistoryEntriesForNode(
  entries: ObservedEventHistoryEntry[],
  node: StoryNodeEvaluation | null,
): ObservedEventHistoryEntry[] {
  if (!node) {
    return [];
  }

  return entries.filter((entry) => {
    if (entry.nodeId && node.nodeId) {
      return entry.nodeId === node.nodeId;
    }

    return Boolean(entry.eventId && node.eventId && entry.eventId === node.eventId);
  });
}

export function buildHistoryNodeMap(
  nodes: StoryNodeEvaluation[],
): Map<string, StoryNodeEvaluation> {
  const map = new Map<string, StoryNodeEvaluation>();

  for (const node of nodes) {
    if (node.nodeId) {
      map.set(node.nodeId, node);
    }
  }

  return map;
}

export function enrichHistoryEntries(
  entries: ObservedEventHistoryEntry[],
  nodes: StoryNodeEvaluation[],
): ObservedEventHistoryEntry[] {
  const uniqueNodeByEventId = buildUniqueNodeByEventId(nodes);

  return entries
    .map((entry) => {
      if (hasHistorySource(entry)) {
        return entry;
      }

      const node = uniqueNodeByEventId.get(entry.eventId);
      if (!node) {
        return entry;
      }

      return {
        ...entry,
        nodeId: node.nodeId,
        sourceModId: node.sourceModId,
        sourceModName: node.sourceModName,
        location: node.location ?? entry.location,
      };
    })
    .filter(hasHistorySource);
}

function buildUniqueNodeByEventId(
  nodes: StoryNodeEvaluation[],
): Map<string, StoryNodeEvaluation> {
  const candidates = new Map<string, StoryNodeEvaluation>();
  const duplicateEventIds = new Set<string>();

  for (const node of nodes) {
    if (!node.eventId) {
      continue;
    }

    if (candidates.has(node.eventId)) {
      duplicateEventIds.add(node.eventId);
      continue;
    }

    candidates.set(node.eventId, node);
  }

  for (const eventId of duplicateEventIds) {
    candidates.delete(eventId);
  }

  return candidates;
}

function hasHistorySource(entry: ObservedEventHistoryEntry): boolean {
  return Boolean(entry.nodeId || entry.sourceModId || entry.sourceModName);
}
