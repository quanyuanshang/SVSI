import { parseConditions } from "./conditionParser";
import { listKnownCharactersFromCatalog } from "./translations";
import type { StoryNodeEvaluation, TranslationCatalog } from "../types/story";

const NPC_COMMAND_NAMES = [
  "speak",
  "move",
  "faceDirection",
  "emote",
  "animate",
  "showFrame",
  "warp",
  "playMusic",
];

const NPC_ID_PATTERN = /^[A-Za-z][A-Za-z0-9_]*$/;

const NPC_FILTER_BLOCKLIST = new Set(
  [
    "end",
    "healer",
    "continue",
    "stop",
    "abort",
    "true",
    "false",
    "yes",
    "no",
    "spring",
    "summer",
    "fall",
    "winter",
    "Town",
    "Farm",
    "Forest",
    "Beach",
  ].map((value) => value.toLowerCase()),
);

let knownNpcCache: Set<string> | null = null;

function getKnownNpcSet(catalog?: TranslationCatalog | null): Set<string> {
  if (!catalog && knownNpcCache) {
    return knownNpcCache;
  }

  const set = new Set(listKnownCharactersFromCatalog(catalog));
  if (!catalog) {
    knownNpcCache = set;
  }

  return set;
}

export function refreshKnownNpcCache(): void {
  knownNpcCache = null;
}

/** Stardew/mod internal NPC ids — excludes dialogue lines, event ids, locations. */
export function isNpcInternalId(raw?: string | null): boolean {
  const cleaned = (raw ?? "").trim();
  if (cleaned.length < 2 || cleaned.length > 32) {
    return false;
  }

  if (!NPC_ID_PATTERN.test(cleaned)) {
    return false;
  }

  if (NPC_FILTER_BLOCKLIST.has(cleaned.toLowerCase())) {
    return false;
  }

  if (/^\d+$/.test(cleaned) || /\d{5,}/.test(cleaned)) {
    return false;
  }

  return true;
}

function escapeForRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function collectFromText(text: string, sink: Set<string>, knownNpcs: Set<string>): void {
  if (!text) {
    return;
  }

  for (const npc of knownNpcs) {
    if (text.indexOf(npc) === -1) {
      continue;
    }

    const pattern = new RegExp(
      `(?<![A-Z])${escapeForRegex(npc)}(?![a-z])`,
    );

    if (pattern.test(text)) {
      sink.add(npc);
    }
  }
}

function collectFromCommands(script: string, sink: Set<string>, knownNpcs: Set<string>): void {
  if (!script) {
    return;
  }

  const commandPattern = new RegExp(
    `\\b(?:${NPC_COMMAND_NAMES.join("|")})\\b\\s+([A-Z][a-zA-Z_]+)`,
    "g",
  );

  let match: RegExpExecArray | null;
  while ((match = commandPattern.exec(script)) !== null) {
    const name = match[1];
    if (knownNpcs.has(name)) {
      sink.add(name);
    }
  }
}

export function extractCharactersFromNode(node: StoryNodeEvaluation): string[] {
  const knownNpcs = getKnownNpcSet();
  const sink = new Set<string>();

  for (const condition of parseConditions(node.rawPreconditions ?? [])) {
    if (
      (
        condition.type === "friendship" ||
        condition.type === "dating" ||
        condition.type === "spouse" ||
        condition.type === "notSpouse" ||
        condition.type === "npcVisibleHere" ||
        condition.type === "npcVisible" ||
        condition.type === "roommate" ||
        condition.type === "notRoommate"
      ) &&
      condition.target &&
      knownNpcs.has(condition.target)
    ) {
      sink.add(condition.target);
    }
  }

  const script = node.rawScriptPreview ?? "";
  collectFromCommands(script, sink, knownNpcs);
  collectFromText(script, sink, knownNpcs);

  if (node.eventId) {
    collectFromText(node.eventId, sink, knownNpcs);
  }

  if (node.rawKey) {
    collectFromText(node.rawKey, sink, knownNpcs);
  }

  for (const ref of node.relatedDialogueRefs ?? []) {
    if (ref.npcName && knownNpcs.has(ref.npcName)) {
      sink.add(ref.npcName);
    }
  }

  for (const ref of node.relatedEventChoiceRefs ?? []) {
    if (ref.npcName && knownNpcs.has(ref.npcName)) {
      sink.add(ref.npcName);
    }
  }

  return Array.from(sink).sort((a, b) => a.localeCompare(b));
}

export function isKnownNpc(name: string, catalog?: TranslationCatalog | null): boolean {
  if (!isNpcInternalId(name)) {
    return false;
  }

  const known = getKnownNpcSet(catalog);
  if (known.has(name)) {
    return true;
  }

  const lower = name.toLowerCase();
  for (const candidate of known) {
    if (candidate.toLowerCase() === lower) {
      return true;
    }
  }

  return false;
}
