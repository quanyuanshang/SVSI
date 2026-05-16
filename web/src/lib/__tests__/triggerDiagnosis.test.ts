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

  it("location compare unwraps Content Patcher location tokens", () => {
    const node = makeNode({ location: "{{FarmHouse}}", rawPreconditions: [] });
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
    expect(item?.reasonZh).toContain("春季");
    expect(item?.reasonZh).toContain("秋季");
  });

  it("keeps supported but unevaluable npc visibility out of unknown parser bucket", () => {
    const node = makeNode({ rawPreconditions: ["p Shane"] });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unknown.find((i) => i.type === "npcVisibleHere");
    expect(item?.conditionRaw).toBe("p Shane");
    expect(item?.reasonZh).not.toContain("绛夊緟鍚庣画琛ュ厖瑙ｆ瀽瑙勫垯");
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

  it("evaluates F as satisfied when today is not a festival", () => {
    const node = makeNode({ rawPreconditions: ["F"] });
    const result = diagnoseEventTrigger(node, {
      ...baseState,
      isFestivalDay: false,
    } as CurrentGameState);
    const item = result.satisfied.find((i) => i.type === "notFestivalDay");
    expect(item?.descriptionZh).toContain("节日");
    expect(item?.conditionRaw).toBe("F");
  });

  it("treats missing festival runtime data for F as non-festival without mojibake", () => {
    const node = makeNode({ rawPreconditions: ["F"] });
    const result = diagnoseEventTrigger(node, {
      ...baseState,
      isFestivalDay: undefined,
    } as CurrentGameState);
    const item = result.satisfied.find((i) => i.type === "notFestivalDay");
    expect(item?.descriptionZh).toBe("今天不是节日");
    expect(item?.reasonZh).toContain("当前未检测到节日");
    expect(result.unknown.some((i) => i.conditionRaw === "F")).toBe(false);
    expect(item?.reasonZh).not.toContain("杩");
  });

  it("shows evaluated DayEvent patch When as a normal event precondition", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "DayEvent",
          value: "wedding",
          isKnown: true,
          passed: false,
          isContextSensitive: true,
          reason: "DayEvent failed: current day events are [], expected wedding.",
          reasonZh: "今日事件不满足：当前为 []，需要 wedding",
        },
      ],
    });

    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unsatisfied.find((entry) => entry.conditionRaw === "DayEvent: wedding");
    expect(item?.descriptionZh).toContain("节日/特殊日");
    expect(item?.descriptionZh).not.toContain("CP When 条件");
    expect(item?.reasonZh).toContain("今日事件不满足");
  });

  it("shows evaluated YearsMarried Query with concrete marriage-year copy", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "Query",
          value: "'{{TheMightyAmondee.CustomTokens/YearsMarried}}' >= 1",
          isKnown: true,
          passed: false,
          isProgressionSensitive: true,
          reason: "Query YearsMarried failed: value is 0.",
        },
      ],
    });

    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unsatisfied.find((entry) => entry.conditionRaw.includes("YearsMarried"));
    expect(item?.descriptionZh).toContain("结婚年数至少 1 年");
    expect(item?.descriptionZh).not.toContain("CP When 条件");
    expect(item?.reasonZh).toContain("当前为 0 年");
    expect(item?.reasonZh).not.toContain("Query");
  });

  it("evaluates day-of-month list u 12 19 20", () => {
    const node = makeNode({ rawPreconditions: ["u 12 19 20"] });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.satisfied.find((i) => i.type === "dayOfMonth");
    expect(item?.descriptionZh).toContain("12");
    expect(item?.descriptionZh).toContain("19");
    expect(item?.descriptionZh).toContain("20");
  });

  it("keeps raw {{CampoutDays}} out of unknown and shows the compact Chinese date copy", () => {
    const node = makeNode({ rawPreconditions: ["{{CampoutDays}}"] });
    const result = diagnoseEventTrigger(node, {
      ...baseState,
      season: "spring",
      day: 12,
    } as CurrentGameState);
    const item = result.satisfied.find((i) => i.conditionRaw === "{{CampoutDays}}");
    expect(item?.descriptionZh).toBe("露营约会日期：春季 12/19/20 或秋季 13/14/18");
    expect(result.unknown.some((i) => i.conditionRaw === "{{CampoutDays}}")).toBe(false);
  });

  it("marks {{CampoutDays}} unsatisfied on known non-campout dates instead of unknown", () => {
    const node = makeNode({ rawPreconditions: ["{{CampoutDays}}"] });
    const result = diagnoseEventTrigger(node, {
      ...baseState,
      season: "fall",
      day: 12,
    } as CurrentGameState);
    expect(result.unsatisfied.some((i) => i.conditionRaw === "{{CampoutDays}}")).toBe(true);
    expect(result.unknown.some((i) => i.conditionRaw === "{{CampoutDays}}")).toBe(false);
  });

  it("keeps other discovered DynamicToken raw fragments out of unknown", () => {
    for (const tokenName of ["FrogDays", "MineDays", "OverlookDays", "PoolDays"]) {
      const raw = `{{${tokenName}}}`;
      const node = makeNode({ rawPreconditions: [raw] });
      const result = diagnoseEventTrigger(node, baseState);
      expect(result.unknown.some((i) => i.conditionRaw === raw)).toBe(false);
      expect([...result.satisfied, ...result.unsatisfied].some((i) => i.conditionRaw === raw)).toBe(true);
    }
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
  });

  it("does not annotate when prereq event is present in the indexed event list", () => {
    const node = makeNode({ rawPreconditions: ["e MaggSamBuildADream"] });
    const result = diagnoseEventTrigger(node, baseState, {
      availableEventIds: new Set(["MaggSamBuildADream"]),
    });
    const item = result.satisfied.find((i) => i.type === "seenEvent");
    expect(item?.reasonZh).not.toContain("鏈湪浜嬩欢绱㈠紩涓");
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

  it("shows non-raw relationship contains copy for known patch-when conditions", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "Relationship:Sebastian |contains=Engaged",
          value: "false",
          isKnown: true,
          passed: true,
          isProgressionSensitive: true,
          reason: "Relationship matched: Sebastian is Dating, expected relationship does not contain Engaged.",
        },
      ],
    });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.satisfied.find((entry) =>
      entry.conditionRaw.includes("Relationship:Sebastian |contains=Engaged"),
    );
    expect(item?.descriptionZh).not.toContain("Relationship:");
    expect(item?.descriptionZh).not.toContain("|contains=");
    expect(item?.descriptionZh).not.toContain("Relationship:Sebastian |contains=Engaged");
  });

  it("evaluates legacy unknown relationship contains patch-when as satisfied when not engaged", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "Relationship:Sebastian |contains=Engaged",
          value: "false",
          isKnown: false,
          reason: "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator.",
        },
      ],
    });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.satisfied.find((entry) =>
      entry.conditionRaw.includes("Relationship:Sebastian |contains=Engaged"),
    );
    expect(item?.descriptionZh).toBe("不能和塞巴斯蒂安处于订婚状态");
    expect(result.unknown.some((entry) => entry.conditionRaw.includes("Relationship:Sebastian"))).toBe(false);
  });

  it("evaluates legacy unknown relationship contains patch-when as unsatisfied when engaged", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "Relationship:Sebastian |contains=Engaged",
          value: "false",
          isKnown: false,
          reason: "Patch-level Content Patcher When condition is not evaluated by the runtime story-state evaluator.",
        },
      ],
    });
    const result = diagnoseEventTrigger(node, { ...baseState, engagedTo: "Sebastian" } as CurrentGameState);
    const item = result.unsatisfied.find((entry) =>
      entry.conditionRaw.includes("Relationship:Sebastian |contains=Engaged"),
    );
    expect(item?.descriptionZh).toBe("不能和塞巴斯蒂安处于订婚状态");
    expect(result.unknown.some((entry) => entry.conditionRaw.includes("Relationship:Sebastian"))).toBe(false);
  });

  it("uses backend-resolved friendship atoms for MinFriendship instead of reporting unknown", () => {
    const node = makeNode({
      rawPreconditions: ["f Wizard {{MinFriendship}}"],
      conditionResult: {
        atomResults: [
          {
            raw: "f Wizard 2500",
            atomType: "Friendship",
            passed: false,
            reason: "Friendship failed: Wizard has 2000, requires at least 2500.",
          },
        ],
      },
    });
    const result = diagnoseEventTrigger(node, { ...baseState, friendship: { ...baseState.friendship, Wizard: 2000 } });
    expect(result.unknown.some((i) => i.conditionRaw.includes("MinFriendship"))).toBe(false);
    expect(result.unsatisfied.some((i) => i.conditionRaw.includes("MinFriendship"))).toBe(true);
  });

  it("does not duplicate runtimeMissing Chinese prefix from backend patch reasons", () => {
    const node = makeNode({
      patchWhenConditions: [
        {
          key: "Pregnant",
          value: "true",
          isKnown: false,
          unknownKind: "runtimeMissing",
          reasonZh: "无法判断：运行时家庭状态未导出（Pregnant）。",
        },
      ],
    });
    const result = diagnoseEventTrigger(node, baseState);
    const item = result.unknown.find((entry) => entry.conditionRaw.includes("Pregnant"));
    expect(item?.reasonZh).toBe("无法判断：运行时家庭状态未导出（Pregnant）。");
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
