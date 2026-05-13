import { extractTimeWindow } from "./timeWindows";
import type { ConditionAtomResult, StoryNodeEvaluation } from "../types/story";

type ConflictSeverity = "warning" | "info";

export interface PotentialConflict {
  id: string;
  severity: ConflictSeverity;
  reason: string;
  nodeAId: string;
  nodeBId: string;
  sharedNpcNames: string[];
  sharedLocation: string | null;
  timeOverlap: boolean;
}

const CANDIDATE_STATUSES = new Set(["Current", "AvailableLater"]);

export function detectPotentialConflicts(
  nodes: StoryNodeEvaluation[],
): PotentialConflict[] {
  const candidates = nodes
    .filter(
      (node) => node.nodeId && node.status && CANDIDATE_STATUSES.has(node.status),
    )
    .map((node) => ({
      node,
      nodeId: node.nodeId as string,
      npcNames: collectNpcNames(node),
    }))
    .filter((entry) => entry.npcNames.size > 0);

  const conflicts: PotentialConflict[] = [];

  for (let leftIndex = 0; leftIndex < candidates.length; leftIndex += 1) {
    for (
      let rightIndex = leftIndex + 1;
      rightIndex < candidates.length;
      rightIndex += 1
    ) {
      const left = candidates[leftIndex];
      const right = candidates[rightIndex];

      const sharedNpcNames = getSharedNpcNames(left.npcNames, right.npcNames);
      if (sharedNpcNames.length === 0) {
        continue;
      }

      if (!isDifferentSourceMod(left.node, right.node)) {
        continue;
      }

      const timeOverlap = hasPossibleTimeOverlap(left.node, right.node);
      if (!timeOverlap) {
        continue;
      }

      const severity: ConflictSeverity =
        left.node.status === "Current" && right.node.status === "Current"
          ? "warning"
          : "info";

      const sharedLocation = getSharedLocation(left.node, right.node);

      conflicts.push({
        id: `${left.nodeId}__${right.nodeId}`,
        severity,
        reason: buildReason(sharedNpcNames, sharedLocation),
        nodeAId: left.nodeId,
        nodeBId: right.nodeId,
        sharedNpcNames,
        sharedLocation,
        timeOverlap,
      });
    }
  }

  return conflicts;
}

function collectNpcNames(node: StoryNodeEvaluation): Set<string> {
  const npcNames = new Set<string>();

  for (const dialogueRef of node.relatedDialogueRefs ?? []) {
    const name = dialogueRef.npcName?.trim();
    if (name) {
      npcNames.add(name);
    }
  }

  for (const atom of node.conditionResult?.atomResults ?? []) {
    if (atom.atomType !== "Friendship") {
      continue;
    }

    for (const npcName of extractNpcNamesFromFriendshipAtom(atom)) {
      npcNames.add(npcName);
    }
  }

  return npcNames;
}

function extractNpcNamesFromFriendshipAtom(atom: ConditionAtomResult): string[] {
  const raw = atom.raw?.trim();
  if (!raw) {
    return [];
  }

  const tokens = raw.split(/\s+/);
  if (tokens.length < 3) {
    return [];
  }

  const startIndex =
    tokens[0] === "f" || tokens[0] === "Friendship" ? 1 : 0;
  const results: string[] = [];

  for (let index = startIndex; index < tokens.length - 1; index += 2) {
    const npcName = tokens[index]?.trim();
    const points = tokens[index + 1]?.trim();

    if (!npcName || !points || !/^-?\d+$/.test(points)) {
      continue;
    }

    results.push(npcName);
  }

  return results;
}

function getSharedNpcNames(left: Set<string>, right: Set<string>): string[] {
  const shared: string[] = [];

  for (const npcName of left) {
    if (right.has(npcName)) {
      shared.push(npcName);
    }
  }

  return shared.sort((a, b) => a.localeCompare(b));
}

function isDifferentSourceMod(
  left: StoryNodeEvaluation,
  right: StoryNodeEvaluation,
): boolean {
  return (
    left.sourceModId !== right.sourceModId ||
    left.sourceModName !== right.sourceModName
  );
}

function hasPossibleTimeOverlap(
  left: StoryNodeEvaluation,
  right: StoryNodeEvaluation,
): boolean {
  const leftWindow = extractTimeWindow(left);
  const rightWindow = extractTimeWindow(right);

  if (!leftWindow || !rightWindow) {
    return true;
  }

  return leftWindow.start <= rightWindow.end && rightWindow.start <= leftWindow.end;
}

function getSharedLocation(
  left: StoryNodeEvaluation,
  right: StoryNodeEvaluation,
): string | null {
  const leftLocation = left.location?.trim();
  const rightLocation = right.location?.trim();

  if (!leftLocation || !rightLocation) {
    return null;
  }

  return leftLocation === rightLocation ? leftLocation : null;
}

function buildReason(sharedNpcNames: string[], sharedLocation: string | null): string {
  const npcSummary = sharedNpcNames.join(", ");

  if (sharedLocation) {
    return `Potential conflict: shared NPC ${npcSummary}, shared location ${sharedLocation}, and possible time overlap.`;
  }

  return `Potential conflict: shared NPC ${npcSummary} and possible time overlap.`;
}
