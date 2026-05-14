import { beforeEach, describe, expect, it } from "vitest";
import { loadTranslationCatalog } from "../translations";
import {
  buildGameStateFromRuntime,
  diagnoseEventTrigger,
  type CurrentGameState,
} from "../triggerDiagnosis";
import type { StoryNodeEvaluation } from "../../types/story";

function makeNode(
  partial: Partial<StoryNodeEvaluation> = {},
): StoryNodeEvaluation {
  return {
    nodeId: "n1",
    eventId: "100001",
    location: "Town",
    rawKey: "100001/f Sam 2000",
    rawPreconditions: [],
    sourceModId: "example.mod",
    ...partial,
  };
}

const baseState: CurrentGameState = {
  year: 1,
  season: "fall",
  day: 12,
  time: 1500,
  location: "Town",
  weather: "sunny",
  friendship: { Sam: 2500, Shane: 1000 },
  dating: ["Sam"],
  marriedTo: null,
  seenEvents: ["MaggSamBuildADream"],
  mailFlags: [],
  conversationTopics: [],
};

describe("diagnoseEventTrigger", () => {
  beforeEach(() => {
    loadTranslationCatalog(null);
  });

  it("classifies satisfied friendship", () => {
    const node = makeNode({ rawPreconditions: ["f Sam 2000"] });
    const result = diagnoseEventTrigger(node, baseState);
    expect(result.satisfied.some((i) => i.conditionRaw === "f Sam 2000")).toBe(true);
    expect(result.unsatisfied).toHaveLength(0);
  });

  it("classifies unsatisfied friendship with Chinese reason", () => {
    const node = makeNode({ rawPreconditions: ["f Shane 5000"] });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unsatisfied.find((i) => i.type === "friendship");
    expect(result.canTrigger).toBe(false);
    expect(item?.reasonZh).toContain("好感度不满足");
  });

  it("flags missing runtime data as unknown not unsatisfied", () => {
    const node = makeNode({ rawPreconditions: ["f Sam 2000"] });
    const result = diagnoseEventTrigger(node, { ...baseState, friendship: undefined });
    expect(result.unknown.some((i) => i.type === "friendship")).toBe(true);
    expect(result.unsatisfied).toHaveLength(0);
  });

  it("respects legacy negated seen-event alias `k`", () => {
    const node = makeNode({ rawPreconditions: ["k MaggSamBuildADream"] });
    const result = diagnoseEventTrigger(node, baseState);
    expect(result.unsatisfied.some((i) => i.type === "notSeenEvent")).toBe(true);
  });

  it("evaluates time window membership", () => {
    const node = makeNode({ rawPreconditions: ["t 1000 1700"] });
    const result = diagnoseEventTrigger(node, baseState);
    expect(result.satisfied.some((i) => i.type === "time")).toBe(true);

    const outside = diagnoseEventTrigger(node, { ...baseState, time: 1800 });
    expect(outside.unsatisfied.some((i) => i.type === "time")).toBe(true);
  });

  it("reports location mismatch in Chinese", () => {
    const node = makeNode({ location: "Beach", rawPreconditions: [] });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unsatisfied.find((i) => i.conditionRaw.startsWith("location "));
    expect(item?.reasonZh).toContain("地点不满足");
    expect(item?.reasonZh).toContain("海滩");
    expect(item?.reasonZh).toContain("鹈鹕镇");
    // Raw English must be present so user can distinguish locations that
    // share the same Chinese label.
    expect(item?.reasonZh).toContain("Beach");
    expect(item?.reasonZh).toContain("Town");
  });

  it("location compare is case-insensitive (Farmhouse vs FarmHouse counts as same place)", () => {
    const node = makeNode({ location: "Farmhouse", rawPreconditions: [] });
    const result = diagnoseEventTrigger(node, {
      ...baseState,
      location: "FarmHouse",
    });
    const item = result.satisfied.find((i) => i.conditionRaw.startsWith("location "));
    expect(item?.reasonZh).toContain("地点满足");
    expect(result.unsatisfied.some((i) => i.conditionRaw.startsWith("location "))).toBe(false);
  });

  it("location truly distinct (Farmhouse interior vs Farm exterior) still mismatches", () => {
    const node = makeNode({ location: "Farmhouse", rawPreconditions: [] });
    const result = diagnoseEventTrigger(node, { ...baseState, location: "Farm" });
    const item = result.unsatisfied.find((i) => i.conditionRaw.startsWith("location "));
    expect(item).toBeTruthy();
    expect(item?.reasonZh).toContain("Farmhouse");
    expect(item?.reasonZh).toContain("Farm");
  });

  it("evaluates spouse conditions from marriedTo/spouse/spouses/engagedTo aliases", () => {
    const node = makeNode({ rawPreconditions: ["O Sebastian"] });
    const states: CurrentGameState[] = [
      { ...baseState, marriedTo: "Sebastian" },
      { ...baseState, spouse: "Sebastian" },
      { ...baseState, spouses: ["Sebastian"] },
      { ...baseState, engagedTo: "Sebastian" },
    ];

    for (const state of states) {
      const result = diagnoseEventTrigger(node, state);
      expect(result.satisfied.some((i) => i.type === "spouse")).toBe(true);
    }
  });

  it("evaluates mod spouse conditions with raw npc ids", () => {
    const node = makeNode({ rawPreconditions: ["O Hovsep"] });
    const result = diagnoseEventTrigger(node, {
      ...baseState,
      marriedTo: "Hovsep",
      spouse: "Hovsep",
      spouses: ["Hovsep"],
    });
    expect(result.satisfied.some((i) => i.type === "spouse")).toBe(true);
    expect(result.unsatisfied.some((i) => i.type === "spouse")).toBe(false);
  });

  it("marks season spring vs fall as unsatisfied using raw season values", () => {
    const node = makeNode({ rawPreconditions: ["s spring"] });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unsatisfied.find((i) => i.type === "season");
    expect(item?.reasonZh).toContain("季节不满足");
    expect(item?.reasonZh).toContain("春季");
    expect(item?.reasonZh).toContain("秋季");
  });

  it("keeps supported but unevaluable npc visibility out of unknown parser bucket", () => {
    const node = makeNode({ rawPreconditions: ["p Shane"] });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unknown.find((i) => i.type === "npcVisibleHere");
    expect(item?.conditionRaw).toBe("p Shane");
    expect(item?.reasonZh).not.toContain("等待后续补充解析规则");
  });

  it("evaluates npcVisibleHere from current visible npc list", () => {
    const node = makeNode({ rawPreconditions: ["p Shane"] });
    const visible = diagnoseEventTrigger(node, {
      ...baseState,
      visibleNpcNamesHere: ["Shane", "Sam"],
    } as CurrentGameState);
    expect(visible.satisfied.some((i) => i.type === "npcVisibleHere")).toBe(true);

    const hidden = diagnoseEventTrigger(node, {
      ...baseState,
      visibleNpcNamesHere: ["Sam"],
    } as CurrentGameState);
    expect(hidden.unsatisfied.some((i) => i.type === "npcVisibleHere")).toBe(true);
  });

  it("evaluates inUpgradedHouse from runtime house state", () => {
    const node = makeNode({ rawPreconditions: ["L"] });
    const inside = diagnoseEventTrigger(node, {
      ...baseState,
      inUpgradedHouse: true,
    } as CurrentGameState);
    expect(inside.satisfied.some((i) => i.type === "inUpgradedHouse")).toBe(true);

    const outside = diagnoseEventTrigger(node, {
      ...baseState,
      inUpgradedHouse: false,
    } as CurrentGameState);
    expect(outside.unsatisfied.some((i) => i.type === "inUpgradedHouse")).toBe(true);
  });

  it("treats missing spouse runtime data as unknown rather than unsatisfied", () => {
    const node = makeNode({ rawPreconditions: ["O Shane"] });
    const state = buildGameStateFromRuntime({
      year: 1,
      season: "spring",
      dayOfMonth: 1,
      dayOfWeek: "Monday",
      time: 600,
      weather: "sunny",
      currentLocation: "Farm",
      playerName: "p",
      friendshipPoints: {},
      seenEvents: [],
      mail: [],
      dialogueAnswers: [],
    });
    const result = diagnoseEventTrigger(node, state);
    expect(result.unsatisfied.some((i) => i.type === "spouse")).toBe(false);
    expect(result.unknown.some((i) => i.type === "spouse")).toBe(true);
  });

  it("treats missing dating runtime data as unknown", () => {
    const node = makeNode({ rawPreconditions: ["D Sam"] });
    const state = buildGameStateFromRuntime({
      year: 1,
      season: "spring",
      dayOfMonth: 1,
      dayOfWeek: "Monday",
      time: 600,
      weather: "sunny",
      currentLocation: "Farm",
      playerName: "p",
      friendshipPoints: {},
      seenEvents: [],
      mail: [],
      dialogueAnswers: [],
    });
    const result = diagnoseEventTrigger(node, state);
    expect(result.unsatisfied.some((i) => i.type === "dating")).toBe(false);
    expect(result.unknown.some((i) => i.type === "dating")).toBe(true);
  });

  it("annotates seenEvent precondition when target is not in the indexed event list", () => {
    const node = makeNode({ rawPreconditions: ["e 11451861"] });
    const result = diagnoseEventTrigger(node, baseState, {
      availableEventIds: new Set(["100001", "MaggSamBuildADream"]),
    });
    const item = result.unsatisfied.find((i) => i.type === "seenEvent");
    expect(item?.reasonZh).toContain("11451861");
    expect(item?.reasonZh).toContain("未在事件索引中");
  });

  it("does not annotate when prereq event is present in the indexed event list", () => {
    const node = makeNode({ rawPreconditions: ["e MaggSamBuildADream"] });
    const result = diagnoseEventTrigger(node, baseState, {
      availableEventIds: new Set(["MaggSamBuildADream"]),
    });
    const item = result.satisfied.find((i) => i.type === "seenEvent");
    expect(item?.reasonZh).not.toContain("未在事件索引中");
  });

  it("buildGameStateFromRuntime maps runtime spouse aliases", () => {
    const state = buildGameStateFromRuntime({
      year: 2,
      season: "spring",
      dayOfMonth: 5,
      dayOfWeek: "Monday",
      time: 900,
      weather: "rainy",
      currentLocation: "Beach",
      playerName: "MockFarmer",
      friendshipPoints: { Sam: 1500 },
      datingNpcNames: ["Sam"],
      spouseName: "Shane",
      engagedTo: "Sebastian",
      seenEvents: ["e1"],
      mail: ["m1"],
      dialogueAnswers: ["a1"],
      installedModIds: ["Pathoschild.ContentPatcher"],
    });
    expect(state.year).toBe(2);
    expect(state.location).toBe("Beach");
    expect(state.friendship?.Sam).toBe(1500);
    expect(state.dating).toContain("Sam");
    expect(state.marriedTo).toBe("Shane");
    expect(state.engagedTo).toBe("Sebastian");
    expect(state.seenEvents).toContain("e1");
    expect(state.installedModIds).toContain("Pathoschild.ContentPatcher");
  });

  it("buildGameStateFromRuntime preserves explicit raw relationship aliases for mod spouses", () => {
    const state = buildGameStateFromRuntime({
      year: 2,
      season: "spring",
      dayOfMonth: 5,
      dayOfWeek: "Monday",
      time: 900,
      weather: "rainy",
      currentLocation: "Beach",
      playerName: "MockFarmer",
      friendshipPoints: {},
      spouseName: "Hovsep",
      spouse: "Hovsep",
      marriedTo: "Hovsep",
      spouses: ["Hovsep"],
      engagedTo: "Sebastian",
      roommate: "Krobus",
      seenEvents: [],
      mail: [],
      dialogueAnswers: [],
    });
    expect(state.marriedTo).toBe("Hovsep");
    expect(state.spouse).toBe("Hovsep");
    expect(state.spouses).toEqual(["Hovsep"]);
    expect(state.engagedTo).toBe("Sebastian");
    expect(state.roommate).toBe("Krobus");
  });

  it("moves known patch-when hearts conditions out of unknown", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "Hearts:Victor",
          value: "10",
          isKnown: true,
          passed: false,
          isProgressionSensitive: true,
          reason: "Hearts failed: Victor has 8 hearts, requires exactly 10.",
        },
      ],
    });
    const result = diagnoseEventTrigger(node, baseState);
    expect(result.unknown.some((i) => i.conditionRaw.includes("Hearts:Victor"))).toBe(false);
    expect(result.unsatisfied.some((i) => i.conditionRaw.includes("Hearts:Victor"))).toBe(true);
  });

  it("keeps unsupported patch-when conditions in unknown", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "FarmerCheater",
          value: "no",
          isKnown: false,
          reason: "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator.",
        },
      ],
    });
    const result = diagnoseEventTrigger(node, baseState);
    expect(result.unknown.some((i) => i.conditionRaw.includes("FarmerCheater"))).toBe(true);
  });
});
