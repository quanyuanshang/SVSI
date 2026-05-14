import { parseConditions } from "./conditionParser";
import { isKnownCharacter, listKnownCharacters } from "./translations";
import type { StoryNodeEvaluation } from "../types/story";

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

const KNOWN_NPCS = new Set(listKnownCharacters());

function escapeForRegex(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function collectFromText(text: string, sink: Set<string>): void {
  if (!text) {
    return;
  }

  for (const npc of KNOWN_NPCS) {
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

function collectFromCommands(script: string, sink: Set<string>): void {
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
    if (KNOWN_NPCS.has(name)) {
      sink.add(name);
    }
  }
}

export function extractCharactersFromNode(node: StoryNodeEvaluation): string[] {
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
      KNOWN_NPCS.has(condition.target)
    ) {
      sink.add(condition.target);
    }
  }

  const script = node.rawScriptPreview ?? "";
  collectFromCommands(script, sink);
  collectFromText(script, sink);

  if (node.eventId) {
    collectFromText(node.eventId, sink);
  }

  if (node.rawKey) {
    collectFromText(node.rawKey, sink);
  }

  for (const ref of node.relatedDialogueRefs ?? []) {
    if (ref.npcName && KNOWN_NPCS.has(ref.npcName)) {
      sink.add(ref.npcName);
    }
  }

  for (const ref of node.relatedEventChoiceRefs ?? []) {
    if (ref.npcName && KNOWN_NPCS.has(ref.npcName)) {
      sink.add(ref.npcName);
    }
  }

  return Array.from(sink).sort((a, b) => a.localeCompare(b));
}

export function isKnownNpc(name: string): boolean {
  return isKnownCharacter(name);
}
