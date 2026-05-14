import { describe, expect, it } from "vitest";
import { extractCharactersFromNode } from "../characters";
import type { StoryNodeEvaluation } from "../../types/story";

function makeNode(
  partial: Partial<StoryNodeEvaluation> = {},
): StoryNodeEvaluation {
  return {
    nodeId: "n1",
    eventId: "100001",
    location: "Town",
    rawKey: "100001",
    rawPreconditions: [],
    rawScriptPreview: "",
    ...partial,
  };
}

describe("extractCharactersFromNode", () => {
  it("picks up NPCs from friendship preconditions", () => {
    const node = makeNode({ rawPreconditions: ["f Sam 2000"] });
    expect(extractCharactersFromNode(node)).toContain("Sam");
  });

  it("picks up NPCs referenced in scripts via speak", () => {
    const node = makeNode({
      rawScriptPreview: "speak Sam \"Hello there\"/end",
    });
    expect(extractCharactersFromNode(node)).toContain("Sam");
  });

  it("picks up NPCs from emote/animate commands", () => {
    const node = makeNode({
      rawScriptPreview: "emote Abigail 32/animate Sebastian leftClick",
    });
    const result = extractCharactersFromNode(node);
    expect(result).toContain("Abigail");
    expect(result).toContain("Sebastian");
  });

  it("filters out unknown words", () => {
    const node = makeNode({
      rawScriptPreview: "speak Randomname Foobar/speak Sam something",
    });
    const result = extractCharactersFromNode(node);
    expect(result).toEqual(["Sam"]);
  });

  it("dedupes and sorts characters", () => {
    const node = makeNode({
      rawScriptPreview: "speak Sam a/speak Sam b/speak Abigail c",
    });
    expect(extractCharactersFromNode(node)).toEqual(["Abigail", "Sam"]);
  });

  it("picks up NPC from event key text", () => {
    const node = makeNode({
      eventId: "MaggSamBuildADream",
    });
    expect(extractCharactersFromNode(node)).toContain("Sam");
  });
});
