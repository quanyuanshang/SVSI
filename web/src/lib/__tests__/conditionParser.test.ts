import { beforeEach, describe, expect, it } from "vitest";
import { loadTranslationCatalog } from "../translations";
import { parseConditionFragment, parseConditions } from "../conditionParser";

describe("parseConditionFragment", () => {
  beforeEach(() => {
    loadTranslationCatalog(null);
  });

  it("parses dating condition `D Sam`", () => {
    const result = parseConditionFragment("D Sam");
    expect(result.type).toBe("dating");
    expect(result.target).toBe("Sam");
    expect(result.negated).toBe(false);
    expect(result.descriptionZh).toContain("约会");
    expect(result.descriptionZh).toContain("山姆");
  });

  it("parses friendship threshold `f Sam 2000`", () => {
    const result = parseConditionFragment("f Sam 2000");
    expect(result.type).toBe("friendship");
    expect(result.target).toBe("Sam");
    expect(result.operator).toBe(">=");
    expect(result.value).toBe(2000);
    expect(result.descriptionZh).toContain("2000");
    expect(result.descriptionZh).toContain("山姆");
  });

  it("parses seen-event condition `e MaggSamBuildADream`", () => {
    const result = parseConditionFragment("e MaggSamBuildADream");
    expect(result.type).toBe("seenEvent");
    expect(result.target).toBe("MaggSamBuildADream");
    expect(result.negated).toBe(false);
    expect(result.descriptionZh).toContain("已触发事件");
  });

  it("parses legacy negated seen-event alias `k SomeEvent`", () => {
    const result = parseConditionFragment("k SomeEvent");
    expect(result.type).toBe("notSeenEvent");
    expect(result.target).toBe("SomeEvent");
    expect(result.negated).toBe(true);
    expect(result.descriptionZh).toContain("未触发事件");
  });

  it("parses negated weather `!w rainy`", () => {
    const result = parseConditionFragment("!w rainy");
    expect(result.type).toBe("notWeather");
    expect(result.target).toBe("rainy");
    expect(result.negated).toBe(true);
    expect(result.descriptionZh).toContain("非");
  });

  it("parses time window `t 1000 1700`", () => {
    const result = parseConditionFragment("t 1000 1700");
    expect(result.type).toBe("time");
    expect(result.operator).toBe("in");
    expect(result.value).toEqual({ start: 1000, end: 1700 });
    expect(result.descriptionZh).toContain("10:00");
    expect(result.descriptionZh).toContain("17:00");
  });

  it("parses `p Shane` as npc visible here instead of dating", () => {
    const result = parseConditionFragment("p Shane");
    expect(result.type).toBe("npcVisibleHere");
    expect(result.target).toBe("Shane");
    expect(result.descriptionZh).toContain("谢恩");
    expect(result.descriptionZh).toContain("该地点");
  });

  it("parses spouse aliases with correct case sensitivity", () => {
    const spouse = parseConditionFragment("O Shane");
    const notSpouse = parseConditionFragment("o Abigail");

    expect(spouse.type).toBe("spouse");
    expect(spouse.descriptionZh).toContain("谢恩");
    expect(notSpouse.type).toBe("notSpouse");
    expect(notSpouse.descriptionZh).toContain("阿比盖尔");
  });

  it("parses upgraded house and spouse bed aliases", () => {
    const upgradedHouse = parseConditionFragment("L");
    const spouseBed = parseConditionFragment("B");

    expect(upgradedHouse.type).toBe("inUpgradedHouse");
    expect(upgradedHouse.descriptionZh).toContain("升级后的农舍");
    expect(spouseBed.type).toBe("spouseBed");
    expect(spouseBed.descriptionZh).toContain("配偶床位");
  });

  it("parses season list `s spring summer`", () => {
    const result = parseConditionFragment("s spring summer");
    expect(result.type).toBe("season");
    expect(result.value).toEqual(["spring", "summer"]);
  });

  it("keeps raw and emits warning-style unknown for unsupported tokens", () => {
    const result = parseConditionFragment("UnparsedToken foobar");
    expect(result.type).toBe("unknown");
    expect(result.raw).toBe("UnparsedToken foobar");
    expect(result.unknownReason).toBeTruthy();
    expect(result.descriptionZh).toContain("未解析条件");
  });

  it("handles whole list via parseConditions", () => {
    const results = parseConditions(["D Sam", "f Sam 2000", "!w rainy"]);
    expect(results.map((r) => r.type)).toEqual(["dating", "friendship", "notWeather"]);
  });
});
