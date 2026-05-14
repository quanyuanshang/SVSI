import type {
  ConditionAtomResult,
  StoryFilterOptions,
  StoryFilterState,
  StoryNodeEvaluation,
  StoryNodeStatus,
} from "../types/story";
import { extractCharactersFromNode } from "./characters";
import { formatStatusLabel, formatLocationZh } from "./format";
import { translateCharacter, translateLocation } from "./translations";

const STATUS_ORDER: StoryNodeStatus[] = [
  "Current",
  "AvailableLater",
  "Locked",
  "Unknown",
  "Triggered",
];

// Group raw values whose translated zh label collides into a single filter
// entry. Picks the alphabetically-first raw as the "representative" stored in
// the option list (so the React key / selection identity is stable). The
// matching map is consumed by applyStoryFilters to keep selecting one zh label
// equivalent to selecting every raw that translates to it (this is how we
// collapse FarmHouse / farmhouse / {{FarmHouse}} → 农舍 visually + behaviorally).
interface DedupedOption {
  representative: string;
  allRaws: string[];
  zhLabel: string;
}

function dedupByTranslation(
  rawValues: Iterable<string>,
  translateZh: (raw: string) => string,
): DedupedOption[] {
  const byZh = new Map<string, { raws: Set<string>; zhLabel: string }>();

  for (const raw of rawValues) {
    if (!raw) {
      continue;
    }

    const zh = translateZh(raw);
    // Group key uses the visible label exactly so two raws with identical
    // Chinese display collapse, while untranslated raws (where zh === raw) keep
    // their own bucket per raw.
    const groupKey = zh;
    const existing = byZh.get(groupKey);
    if (existing) {
      existing.raws.add(raw);
    } else {
      byZh.set(groupKey, { raws: new Set([raw]), zhLabel: zh });
    }
  }

  return Array.from(byZh.values()).map((entry) => {
    const raws = Array.from(entry.raws).sort((a, b) => a.localeCompare(b));
    return {
      representative: raws[0],
      allRaws: raws,
      zhLabel: entry.zhLabel,
    };
  });
}

function sortByZh(options: DedupedOption[]): DedupedOption[] {
  return options
    .slice()
    .sort((a, b) => a.zhLabel.localeCompare(b.zhLabel, "zh-CN"));
}

// Map of "selected raw representative" → set of every raw that translates to
// the same zh label. Built once per filter render so applyStoryFilters can keep
// using raw comparisons (per AGENTS.md) while the UI shows a single de-dup'd
// label.
function buildEquivalenceMap(options: DedupedOption[]): Map<string, Set<string>> {
  const map = new Map<string, Set<string>>();
  for (const option of options) {
    const equivalents = new Set(option.allRaws);
    for (const raw of option.allRaws) {
      map.set(raw, equivalents);
    }
  }
  return map;
}

export function getAvailableFilterOptions(
  nodes: StoryNodeEvaluation[],
): StoryFilterOptions {
  const statuses = new Set<StoryNodeStatus>();
  const modNames = new Set<string>();
  const locationRaws = new Set<string>();
  const npcRaws = new Set<string>();

  for (const node of nodes) {
    if (node.status) {
      statuses.add(node.status);
    }

    if (node.sourceModName) {
      modNames.add(node.sourceModName);
    }

    if (node.location) {
      locationRaws.add(node.location);
    }

    for (const npcName of collectNpcNames(node)) {
      npcRaws.add(npcName);
    }
  }

  const locationOptions = sortByZh(
    dedupByTranslation(locationRaws, (raw) => translateLocation(raw).zh),
  );
  const npcOptions = sortByZh(
    dedupByTranslation(npcRaws, (raw) => translateCharacter(raw).zh),
  );

  return {
    statuses: STATUS_ORDER.filter((status) => statuses.has(status)),
    modNames: Array.from(modNames).sort((a, b) => a.localeCompare(b)),
    locations: locationOptions.map((option) => option.representative),
    npcNames: npcOptions.map((option) => option.representative),
    locationEquivalents: buildEquivalenceMap(locationOptions),
    npcEquivalents: buildEquivalenceMap(npcOptions),
  };
}

// Expand each selected raw value back to the set of all raws that share the
// same translated label. Without an equivalence map (older callers) the
// behavior degrades to exact-raw matching so logic stays correct.
function expandSelection(
  selected: ReadonlySet<string>,
  equivalents?: ReadonlyMap<string, ReadonlySet<string>>,
): Set<string> {
  const expanded = new Set<string>();
  for (const value of selected) {
    expanded.add(value);
    const group = equivalents?.get(value);
    if (group) {
      for (const member of group) {
        expanded.add(member);
      }
    }
  }
  return expanded;
}

export function applyStoryFilters(
  nodes: StoryNodeEvaluation[],
  filters: StoryFilterState,
  options?: Pick<StoryFilterOptions, "locationEquivalents" | "npcEquivalents">,
): StoryNodeEvaluation[] {
  const search = filters.searchText.trim().toLocaleLowerCase();
  const expandedLocations = expandSelection(
    filters.selectedLocations,
    options?.locationEquivalents,
  );
  const expandedNpcs = expandSelection(
    filters.selectedNpcNames,
    options?.npcEquivalents,
  );

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
      expandedLocations.size > 0 &&
      (!node.location || !expandedLocations.has(node.location))
    ) {
      return false;
    }

    if (expandedNpcs.size > 0) {
      const nodeNpcNames = collectNpcNames(node);
      const hasNpcMatch = Array.from(expandedNpcs).some((npcName) =>
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
  const npcNames = new Set<string>(extractCharactersFromNode(node));

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
  const npcNames = Array.from(collectNpcNames(node));

  return [
    node.eventId ?? "",
    node.sourceModName ?? "",
    node.location ?? "",
    formatLocationZh(node.location),
    node.status ?? "",
    formatStatusLabel(node.status),
    node.statusReason ?? "",
    ...npcNames,
    ...npcNames.map((name) => translateCharacter(name).zh),
    ...rawConditions,
  ]
    .join(" ")
    .toLocaleLowerCase();
}
