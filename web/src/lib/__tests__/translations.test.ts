import { beforeEach, describe, expect, it } from "vitest";
import {
  getLocationDebugRows,
  getUntranslatedNames,
  isKnownCharacter,
  loadModTranslations,
  loadTranslationCatalog,
  resolveDisplayName,
  translate,
  translateCharacter,
  translateLocation,
  translateName,
} from "../translations";

describe("translations", () => {
  beforeEach(() => {
    loadTranslationCatalog(null);
  });

  it("translates custom train station location to Chinese", () => {
    const translation = translateLocation("Custom_GrampletonSuburbsTrainStation");
    expect(translation.untranslated).toBe(false);
    expect(translation.zh).toBe("格兰普顿郊区火车站");
    expect(translation.raw).toBe("Custom_GrampletonSuburbsTrainStation");
  });

  it("keeps resource-informed fallback names for remaining SVE Grampleton locations", () => {
    expect(translateLocation("Custom_GrampletonFields").zh).toBe("格兰普顿平原");
    expect(translateLocation("Custom_GrampletonSuburbs").zh).toBe("格兰普顿郊区");
    expect(translateLocation("Custom_GrampletonTrainStation").zh).toBe("格兰普顿火车站");
  });

  it("translates FarmHouse to 农舍", () => {
    expect(translateLocation("FarmHouse").zh).toBe("农舍");
  });

  it("provides minimal fallback translations for remaining custom names", () => {
    expect(translateLocation("Custom_MarnieShed").zh).toBe("玛妮的小屋");
    expect(translateLocation("Custom_FirstSlashGuestRoom").zh).toBe("First Slash 客房");
    expect(translateLocation("Custom_Woods1").zh).toBe("七宗罪森林 1");
    expect(translateLocation("Custom_Woods2").zh).toBe("七宗罪森林 2");
    expect(translateCharacter("Uriel").zh).toBe("乌列");
  });

  it("prefers mod resource translations over fallback map", () => {
    loadTranslationCatalog({
      entries: [
        {
          category: "npc",
          raw: "Pelette",
          zh: "佩莱特",
          source: "mod-i18n",
          sourceModId: "example.mod",
          sourcePath: "i18n/zh-CN.json",
        },
      ],
    });

    const translation = translateCharacter("Pelette", "example.mod");
    expect(translation.zh).toBe("佩莱特");
    expect(translation.source).toBe("mod-i18n");
    expect(loadModTranslations("example.mod")).toHaveLength(1);
  });

  it("falls back to raw without showing untranslated marker in normal display", () => {
    const translation = translateLocation("UnknownLocation_XYZ");
    expect(translation.untranslated).toBe(true);
    expect(translation.zh).toBe("UnknownLocation_XYZ");
    expect(translation.raw).toBe("UnknownLocation_XYZ");
    expect(getUntranslatedNames()).toEqual([
      {
        raw: "UnknownLocation_XYZ",
        category: "location",
        sourceMod: undefined,
      },
    ]);
  });

  it("exposes generic translate() entrypoint per namespace", () => {
    expect(translate("Beach", "location").zh).toBe("海滩");
    expect(translate("Sam", "character").zh).toBe("山姆");
    expect(translate("rainy", "weather").zh).toBe("雨天");
    expect(translate("spring", "season").zh).toBe("春季");
  });

  it("provides translateName() helpers", () => {
    expect(translateName("Shane", "npc")).toBe("谢恩");
    expect(translateName("Town", "location")).toBe("鹈鹕镇");
  });

  it("returns translation source for debug use", () => {
    loadTranslationCatalog({
      entries: [
        {
          category: "location",
          raw: "Custom_GrampletonSuburbsTrainStation",
          zh: "格兰普顿郊区火车站",
          source: "content",
          sourceModId: "sve",
          sourcePath: "content.json",
        },
      ],
    });

    const translation = resolveDisplayName(
      "Custom_GrampletonSuburbsTrainStation",
      "location",
      "sve",
    );
    expect(translation.source).toBe("content");
    expect(translation.sourcePath).toBe("content.json");
  });

  it("falls back to a mod-scoped location entry when no source mod is passed", () => {
    loadTranslationCatalog({
      entries: [
        {
          category: "location",
          raw: "Custom_Woods2",
          zh: "隐秘森林",
          source: "mod-i18n",
          sourceModId: "example.sds",
          sourceModName: "Seven Deadly Sins",
          sourcePath: "i18n/default.json",
        },
      ],
    });

    const translation = translateLocation("Custom_Woods2");
    expect(translation.zh).toBe("隐秘森林");
    expect(translation.source).toBe("mod-i18n");
    expect(translation.confidence).toBe("high");
  });

  it("uses resource translations for marnie shed when the catalog provides them", () => {
    loadTranslationCatalog({
      entries: [
        {
          category: "location",
          raw: "Custom_MarnieShed",
          zh: "玛妮的小屋",
          source: "structured-content",
          sourceModId: "sve",
          sourcePath: "code/Locations/WorldMap.json",
        },
      ],
    });

    const translation = translateLocation("Custom_MarnieShed", "sve");
    expect(translation.zh).toBe("玛妮的小屋");
    expect(translation.source).toBe("structured-content");
    expect(translation.confidence).toBe("high");
  });

  it("keeps fallback-only location metadata in debug rows", () => {
    const rows = getLocationDebugRows();
    const woods1 = rows.find((row) => row.raw === "Custom_Woods1");
    expect(woods1).toBeDefined();
    expect(woods1?.zh).toBe("七宗罪森林 1");
    expect(woods1?.sourceType).toBe("fallback");
    expect(woods1?.confidence).toBe("low");
  });

  it("identifies known NPC names", () => {
    expect(isKnownCharacter("Sam")).toBe(true);
    expect(isKnownCharacter("NotANpc")).toBe(false);
  });

  it("strips Content Patcher token wrappers when looking up a location", () => {
    const direct = translateLocation("FarmHouse");
    const wrapped = translateLocation("{{FarmHouse}}");
    const wrappedWithSpaces = translateLocation("{{ FarmHouse }}");
    expect(direct.zh).toBe("农舍");
    expect(wrapped.zh).toBe("农舍");
    expect(wrappedWithSpaces.zh).toBe("农舍");
    expect(wrapped.untranslated).toBe(false);
    expect(wrapped.raw).toBe("{{FarmHouse}}");
  });

  it("merges case-only variants via normalized fallback lookup", () => {
    expect(translateLocation("FarmHouse").zh).toBe("农舍");
    expect(translateLocation("farmhouse").zh).toBe("农舍");
    expect(translateLocation("Farmhouse").zh).toBe("农舍");
    expect(translateLocation("FARMHOUSE").zh).toBe("农舍");
  });

  it("adds Chinese fallbacks for common SVE/Ridgeside characters", () => {
    expect(translateCharacter("Andy").zh).toBe("安迪");
    expect(translateCharacter("Claire").zh).toBe("克莱尔");
    expect(translateCharacter("Lance").zh).toBe("兰斯");
    expect(translateCharacter("Sophia").zh).toBe("索菲娅");
    expect(translateCharacter("Victor").zh).toBe("维克多");
    expect(translateCharacter("Marlon").zh).toBe("马龙");
    expect(translateCharacter("Morris").zh).toBe("莫里斯");
  });
});
