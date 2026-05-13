import type {
  ConditionAtomResult,
  StoryFilterOptions,
  StoryFilterState,
  StoryNodeEvaluation,
  StoryNodeStatus,
} from "../types/story";

const STATUS_ORDER: StoryNodeStatus[] = [
  "Current",
  "AvailableLater",
  "Locked",
  "Unknown",
  "Triggered",
];

export function getAvailableFilterOptions(
  nodes: StoryNodeEvaluation[],
): StoryFilterOptions {
  const statuses = new Set<StoryNodeStatus>();
  const modNames = new Set<string>();
  const locations = new Set<string>();
  const npcNames = new Set<string>();

  for (const node of nodes) {
    if (node.status) {
      statuses.add(node.status);
    }

    if (node.sourceModName) {
      modNames.add(node.sourceModName);
    }

    if (node.location) {
      locations.add(node.location);
    }

    for (const dialogueRef of node.relatedDialogueRefs ?? []) {
      if (dialogueRef.npcName) {
        npcNames.add(dialogueRef.npcName);
      }
    }

    for (const eventChoiceRef of node.relatedEventChoiceRefs ?? []) {
      if (eventChoiceRef.npcName) {
        npcNames.add(eventChoiceRef.npcName);
      }
    }

    for (const atom of node.conditionResult?.atomResults ?? []) {
      if (atom.atomType === "Friendship") {
        for (const npcName of extractNpcNamesFromFriendshipAtom(atom)) {
          npcNames.add(npcName);
        }
      }
    }
  }

  return {
    statuses: STATUS_ORDER.filter((status) => statuses.has(status)),
    modNames: Array.from(modNames).sort((a, b) => a.localeCompare(b)),
    locations: Array.from(locations).sort((a, b) => a.localeCompare(b)),
    npcNames: Array.from(npcNames).sort((a, b) => a.localeCompare(b)),
  };
}

export function applyStoryFilters(
  nodes: StoryNodeEvaluation[],
  filters: StoryFilterState,
): StoryNodeEvaluation[] {
  const search = filters.searchText.trim().toLocaleLowerCase();

  return nodes.filter((node) => {
    if (filters.hideTriggered && node.status === "Triggered") {
      return false;
    }

    if (
      filters.selectedStatuses.size > 0 &&
      (!node.status || !filters.selectedStatuses.has(node.status))
    ) {
      return false;
    }

    if (
      filters.selectedModNames.size > 0 &&
      (!node.sourceModName || !filters.selectedModNames.has(node.sourceModName))
    ) {
      return false;
    }

    if (
      filters.selectedLocations.size > 0 &&
      (!node.location || !filters.selectedLocations.has(node.location))
    ) {
      return false;
    }

    if (filters.selectedNpcNames.size > 0) {
      const nodeNpcNames = collectNpcNames(node);
      const hasNpcMatch = Array.from(filters.selectedNpcNames).some((npcName) =>
        nodeNpcNames.has(npcName),
      );

      if (!hasNpcMatch) {
        return false;
      }
    }

    if (!search) {
      return true;
    }

    return buildSearchableText(node).includes(search);
  });
}

function collectNpcNames(node: StoryNodeEvaluation): Set<string> {
  const npcNames = new Set<string>();

  for (const dialogueRef of node.relatedDialogueRefs ?? []) {
    if (dialogueRef.npcName) {
      npcNames.add(dialogueRef.npcName);
    }
  }

  for (const eventChoiceRef of node.relatedEventChoiceRefs ?? []) {
    if (eventChoiceRef.npcName) {
      npcNames.add(eventChoiceRef.npcName);
    }
  }

  for (const atom of node.conditionResult?.atomResults ?? []) {
    if (atom.atomType === "Friendship") {
      for (const npcName of extractNpcNamesFromFriendshipAtom(atom)) {
        npcNames.add(npcName);
      }
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
    const npcName = tokens[index];
    const points = tokens[index + 1];

    if (!npcName || !/^-?\d+$/.test(points)) {
      continue;
    }

    results.push(npcName);
  }

  return results;
}

function buildSearchableText(node: StoryNodeEvaluation): string {
  const rawConditions = (node.conditionResult?.atomResults ?? [])
    .map((atom) => atom.raw ?? "")
    .filter(Boolean);

  return [
    node.eventId ?? "",
    node.sourceModName ?? "",
    node.location ?? "",
    node.statusReason ?? "",
    ...rawConditions,
  ]
    .join(" ")
    .toLocaleLowerCase();
}
