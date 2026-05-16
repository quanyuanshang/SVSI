import { describe, expect, it } from "vitest";
import {
  buildStoryGraph,
  buildStorylineSections,
  buildTodayActionGroups,
} from "../storyGraph";
import type { ObservedEventHistoryEntry } from "../../types/history";
import type { StoryNodeEvaluation } from "../../types/story";

function makeNode(overrides: Partial<StoryNodeEvaluation>): StoryNodeEvaluation {
  return {
    nodeId: overrides.nodeId ?? `node-${overrides.eventId ?? "0"}`,
    eventId: overrides.eventId ?? "0",
    sourceModId: "test.mod",
    sourceModName: "Test Mod",
    location: "Town",
    rawKey: overrides.eventId ?? "0",
    rawPreconditions: [],
    unknownFragments: [],
    patchWhenConditions: [],
    rawScriptPreview: "",
    status: "Current",
    statusReason: "",
    conditionResult: { atomResults: [] },
    relatedDialogueRefs: [],
    relatedEventChoiceRefs: [],
    ...overrides,
  };
}

describe("storyGraph", () => {
  it("derives prerequisites and dependents from seen-event conditions", () => {
    const graph = buildStoryGraph([
      makeNode({ eventId: "100", status: "Triggered" }),
      makeNode({
        eventId: "200",
        status: "Current",
        rawPreconditions: ["e 100", "HasSeenEvent 300"],
        conditionResult: {
          atomResults: [
            { atomType: "EventSeen", raw: "HostSeenEvent 400", passed: null },
          ],
        },
      }),
      makeNode({ eventId: "300", status: "Locked" }),
      makeNode({ eventId: "400", status: "Unknown" }),
    ]);

    const current = graph.nodesByKey.get("node-200")!;
    expect(current.prerequisites.map((item) => item.eventId).sort()).toEqual([
      "100",
      "300",
      "400",
    ]);

    const upstream = graph.nodesByKey.get("node-100")!;
    expect(upstream.dependents.map((item) => item.eventId)).toEqual(["200"]);
  });

  it("keeps partial graph warnings for unresolved prerequisite references", () => {
    const graph = buildStoryGraph([
      makeNode({
        eventId: "200",
        status: "Unknown",
        rawPreconditions: ["e missingEvent"],
        unknownFragments: ["CustomToken:Foo"],
        patchWhenConditions: [
          {
            key: "HasMod",
            value: "{{SomeExternalToken}}",
            isKnown: false,
            unknownKind: "externalTokenMissing",
          },
        ],
      }),
    ]);

    const node = graph.nodesByKey.get("node-200")!;
    expect(node.prerequisites).toEqual([
      expect.objectContaining({ eventId: "missingEvent", node: null }),
    ]);
    expect(node.unresolvedConditions).toEqual(
      expect.arrayContaining(["CustomToken:Foo", "HasMod: {{SomeExternalToken}}"]),
    );
  });

  it("uses history to identify the latest triggered event in a filtered storyline", () => {
    const entries: ObservedEventHistoryEntry[] = [
      {
        eventId: "100",
        nodeId: "node-100",
        firstSeenGameDate: { year: 1, season: "spring", dayOfMonth: 2, time: 900 },
      },
      {
        eventId: "200",
        nodeId: "node-200",
        firstSeenGameDate: { year: 1, season: "spring", dayOfMonth: 7, time: 1100 },
      },
    ];
    const graph = buildStoryGraph(
      [
        makeNode({ eventId: "100", status: "Triggered" }),
        makeNode({ eventId: "200", status: "Triggered" }),
        makeNode({ eventId: "300", status: "Current", rawPreconditions: ["e 200"] }),
      ],
      entries,
    );

    const sections = buildStorylineSections(graph, Array.from(graph.nodesByKey.values()));

    expect(sections.latestTriggered?.eventId).toBe("200");
    expect(sections.current.map((item) => item.eventId)).toEqual(["300"]);
  });

  it("prioritizes today action groups by downstream unlock count", () => {
    const graph = buildStoryGraph([
      makeNode({ eventId: "100", status: "Current" }),
      makeNode({ eventId: "200", status: "Current" }),
      makeNode({ eventId: "300", status: "Locked", rawPreconditions: ["e 200"] }),
      makeNode({ eventId: "400", status: "Locked", rawPreconditions: ["e 200"] }),
    ]);

    const groups = buildTodayActionGroups(Array.from(graph.nodesByKey.values()), {
      "node-100": 1,
    });

    expect(groups.ready.map((item) => item.eventId)).toEqual(["200", "100"]);
    expect(groups.conflicts.map((item) => item.eventId)).toEqual(["100"]);
    expect(groups.locked.map((item) => item.eventId)).toEqual(["300", "400"]);
  });

  it("blocks PlayerKilled behind PlayerDied even when the base status says current", () => {
    const graph = buildStoryGraph(
      [
        makeNode({
          eventId: "PlayerKilled",
          status: "Current",
        }),
      ],
      [],
      {},
      {
        year: 1,
        season: "spring",
        dayOfMonth: 1,
        dayOfWeek: "Mon",
        time: 900,
        weather: "sunny",
        currentLocation: "Town",
        playerName: "Farmer",
        friendshipPoints: {},
        seenEvents: [],
        mail: [],
        dialogueAnswers: [],
      },
    );

    const node = graph.nodesByKey.get("node-PlayerKilled")!;
    const groups = buildTodayActionGroups([node]);

    expect(node.isBlocked).toBe(true);
    expect(node.status).toBe("Locked");
    expect(node.blockReason).toContain("PlayerDied");
    expect(node.prerequisites.map((item) => item.eventId)).toContain("PlayerDied");
    expect(groups.ready).toEqual([]);
    expect(groups.locked.map((item) => item.eventId)).toEqual(["PlayerKilled"]);
  });

  it("keeps PlayerKilled current when PlayerDied has been seen", () => {
    const graph = buildStoryGraph(
      [makeNode({ eventId: "PlayerKilled", status: "Current" })],
      [],
      {},
      {
        year: 1,
        season: "spring",
        dayOfMonth: 1,
        dayOfWeek: "Mon",
        time: 900,
        weather: "sunny",
        currentLocation: "Town",
        playerName: "Farmer",
        friendshipPoints: {},
        seenEvents: ["PlayerDied"],
        mail: [],
        dialogueAnswers: [],
      },
    );

    const node = graph.nodesByKey.get("node-PlayerKilled")!;
    const groups = buildTodayActionGroups([node]);

    expect(node.isBlocked).toBe(false);
    expect(node.status).toBe("Current");
    expect(groups.ready.map((item) => item.eventId)).toEqual(["PlayerKilled"]);
  });
});
