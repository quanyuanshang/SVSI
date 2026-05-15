import { beforeEach, describe, expect, it } from "vitest";
import { refreshKnownNpcCache } from "../characters";
import { applyStoryFilters, getAvailableFilterOptions } from "../storyFilters";
import {
  formatNpcFilterLabel,
  listKnownCharactersFromCatalog,
  loadTranslationCatalog,
} from "../translations";
import type {
  StoryFilterState,
  StoryNodeEvaluation,
  StoryNodeStatus,
} from "../../types/story";

function makeNode(overrides: Partial<StoryNodeEvaluation>): StoryNodeEvaluation {
  return {
    nodeId: `node-${Math.random().toString(36).slice(2)}`,
    eventId: "0",
    sourceModName: "Test Mod",
    location: "Town",
    rawKey: "0",
    rawPreconditions: [],
    unknownFragments: [],
    rawScriptPreview: "",
    patchWhenConditions: [],
    status: "Current" as StoryNodeStatus,
    statusReason: "",
    conditionResult: { passed: true, hasUnknown: false, atomResults: [] },
    evidenceRefs: [],
    relatedDialogueRefs: [],
    relatedEventChoiceRefs: [],
    ...overrides,
  };
}

function makeFilters(overrides?: Partial<StoryFilterState>): StoryFilterState {
  return {
    selectedStatuses: new Set(),
    selectedModNames: new Set(),
    selectedLocations: new Set(),
    selectedNpcNames: new Set(),
    hideTriggered: false,
    hideNonTriggerable: true,
    searchText: "",
    ...overrides,
  };
}

describe("storyFilters - de-duplication by translated label", () => {
  beforeEach(() => {
    loadTranslationCatalog(null);
    refreshKnownNpcCache();
  });

  it("collapses FarmHouse / farmhouse / {{FarmHouse}} into a single location entry", () => {
    const nodes = [
      makeNode({ location: "FarmHouse" }),
      makeNode({ location: "farmhouse" }),
      makeNode({ location: "{{FarmHouse}}" }),
      makeNode({ location: "Town" }),
    ];

    const options = getAvailableFilterOptions(nodes);

    expect(options.locations).toHaveLength(2);
    // Selecting any one representative pulls in the equivalent raws.
    const reps = new Set(options.locations);
    expect(reps.has("Town") || reps.has("town")).toBe(true);
    const farmhouseRep = options.locations.find((raw) =>
      ["FarmHouse", "Farmhouse", "farmhouse", "{{FarmHouse}}"].includes(raw),
    );
    expect(farmhouseRep).toBeDefined();
    const equivalents = options.locationEquivalents?.get(farmhouseRep!);
    expect(equivalents).toBeDefined();
    expect(equivalents!.size).toBe(3);
  });

  it("filters every raw location that maps to the same zh label when one representative is selected", () => {
    const nodes = [
      makeNode({ eventId: "1", location: "FarmHouse" }),
      makeNode({ eventId: "2", location: "farmhouse" }),
      makeNode({ eventId: "3", location: "{{FarmHouse}}" }),
      makeNode({ eventId: "4", location: "Town" }),
    ];

    const options = getAvailableFilterOptions(nodes);
    const farmhouseRep = options.locations.find(
      (raw) =>
        raw === "FarmHouse" ||
        raw === "Farmhouse" ||
        raw === "farmhouse" ||
        raw === "{{FarmHouse}}",
    )!;

    const filters = makeFilters({
      selectedLocations: new Set([farmhouseRep]),
    });

    const filtered = applyStoryFilters(nodes, filters, {
      locationEquivalents: options.locationEquivalents,
      npcEquivalents: options.npcEquivalents,
    });

    expect(filtered.map((node) => node.eventId).sort()).toEqual(["1", "2", "3"]);
  });

  it("de-dupes NPC filter entries that share the same zh translation", () => {
    loadTranslationCatalog({
      entries: [
        {
          raw: "shane",
          zh: "谢恩",
          category: "npc",
          source: "content",
          sourcePath: "Mods/Test/Data/Characters/shane.json",
        },
      ],
    });
    refreshKnownNpcCache();

    const nodes = [
      makeNode({
        relatedDialogueRefs: [{ npcName: "Shane" }],
      }),
      makeNode({
        relatedDialogueRefs: [{ npcName: "shane" }],
      }),
    ];

    const options = getAvailableFilterOptions(nodes);
    const shaneRepresentatives = options.npcNames.filter((raw) => {
      const group = options.npcEquivalents?.get(raw);
      return group?.has("Shane") || group?.has("shane");
    });

    expect(shaneRepresentatives).toHaveLength(1);
    const equivalents = options.npcEquivalents?.get(shaneRepresentatives[0]!);
    expect(equivalents?.has("Shane")).toBe(true);
    expect(equivalents?.has("shane")).toBe(true);
  });

  it("keeps untranslated raws as their own entry without crashing", () => {
    const nodes = [
      makeNode({ location: "Custom_AndyHouse" }),
      makeNode({ location: "Custom_AndyHouse" }),
      makeNode({ location: "AdventureGuild" }),
    ];

    const options = getAvailableFilterOptions(nodes);
    expect(options.locations.length).toBeGreaterThanOrEqual(1);
    expect(options.locations).toContain("AdventureGuild");
  });

  it("dedupes catalog-only translations only after the catalog is loaded", () => {
    // This guards the race that broke live filtering: when
    // getAvailableFilterOptions runs before loadTranslationCatalog, mod-only
    // locations (no entry in FALLBACK_LOCATIONS) stay in their own zh buckets,
    // so the equivalence map collapses nothing and clicking a checkbox only
    // matches the one rep raw. Loading the catalog first must merge them.
    const nodes = [
      makeNode({ eventId: "1", location: "Custom_AuroraVineyard" }),
      makeNode({ eventId: "2", location: "custom_auroravineyard" }),
    ];

    loadTranslationCatalog(null);
    const beforeCatalog = getAvailableFilterOptions(nodes);
    // No fallback for this mod location, so each casing stays distinct.
    expect(beforeCatalog.locations.length).toBe(2);

    loadTranslationCatalog({
      entries: [
        {
          raw: "Custom_AuroraVineyard",
          zh: "极光葡萄园",
          category: "location",
          source: "ContentPatcher",
        },
        {
          raw: "custom_auroravineyard",
          zh: "极光葡萄园",
          category: "location",
          source: "ContentPatcher",
        },
      ],
    });

    const afterCatalog = getAvailableFilterOptions(nodes);
    expect(afterCatalog.locations.length).toBe(1);
    const rep = afterCatalog.locations[0];
    const equivalents = afterCatalog.locationEquivalents?.get(rep);
    expect(equivalents?.size).toBe(2);

    const filters = makeFilters({ selectedLocations: new Set([rep]) });
    const filtered = applyStoryFilters(nodes, filters, {
      locationEquivalents: afterCatalog.locationEquivalents,
      npcEquivalents: afterCatalog.npcEquivalents,
    });
    expect(filtered.map((node) => node.eventId).sort()).toEqual(["1", "2"]);
  });

  it("includes Sebastian-only precondition events in NPC filters and matches them when selected", () => {
    const node = makeNode({
      eventId: "MaggSebGame407092025",
      rawKey:
        "MaggSebGame407092025/D Sebastian/e MaggSebGame3Farmer07092025/A MaggSebGame4/t 1900 2400",
      rawPreconditions: [
        "D Sebastian",
        "e MaggSebGame3Farmer07092025",
        "A MaggSebGame4",
        "t 1900 2400",
      ],
      rawScriptPreview: "speak Sebastian \"test\"/end",
    });

    const options = getAvailableFilterOptions([node]);
    expect(options.npcNames).toContain("Sebastian");

    const filtered = applyStoryFilters(
      [node],
      makeFilters({ selectedNpcNames: new Set(["Sebastian"]) }),
      {
        locationEquivalents: options.locationEquivalents,
        npcEquivalents: options.npcEquivalents,
      },
    );

    expect(filtered.map((item) => item.eventId)).toEqual(["MaggSebGame407092025"]);
  });

  it("does not add dialogue lines, event ids, or non-npc catalog entries to NPC filter options", () => {
    loadTranslationCatalog({
      entries: [
        { raw: "Sebastian", zh: "塞巴斯蒂安", category: "npc", source: "test" },
        { raw: "Sam", zh: "山姆", category: "npc", source: "test" },
        { raw: "Alex", zh: "亚历克斯", category: "npc", source: "test" },
        { raw: "Custom_SDS", zh: "某地点", category: "location", source: "test" },
        { raw: "MaggSebGame4", zh: "错误对话", category: "item", source: "test" },
      ],
    });

    const node = makeNode({
      eventId: "MaggSebGame4",
      rawKey: "end/healer",
      rawPreconditions: ["A MaggSebGame4"],
      rawScriptPreview: 'speak Sebastian "hello farmer"/end',
      relatedDialogueRefs: [{ npcName: "Sebastian", previewText: "hello farmer" }],
    });

    const options = getAvailableFilterOptions([node], {
      runtimeState: {
        year: 1,
        season: "spring",
        dayOfMonth: 1,
        dayOfWeek: "Mon",
        time: 900,
        weather: "Sun",
        currentLocation: "Town",
        playerName: "Farmer",
        friendshipPoints: { Sebastian: 2500, Sam: 500 },
        seenEvents: [],
        mail: [],
        dialogueAnswers: [],
      },
      translationCatalog: {
        entries: [
          { raw: "Sebastian", zh: "塞巴斯蒂安", category: "npc", source: "test" },
          { raw: "Sam", zh: "山姆", category: "npc", source: "test" },
          { raw: "Alex", zh: "亚历克斯", category: "npc", source: "test" },
        ],
      },
    });

    expect(options.npcNames).toEqual(expect.arrayContaining(["Sebastian", "Sam"]));
    expect(options.npcNames).not.toContain("MaggSebGame4");
    expect(options.npcNames).not.toContain("end");
    expect(options.npcNames).not.toContain("healer");
    expect(options.npcNames).not.toContain("Custom_SDS");
    expect(options.npcNames).not.toContain("hello farmer this is dialogue");
  });

  it("ignores dialogue-file catalog rows when building NPC filter options", () => {
    const catalog = {
      entries: [
        { raw: "Sebastian", zh: "塞巴斯蒂安", category: "npc", source: "vanilla-export" },
        {
          raw: "Mon",
          zh: "这是周一的对话内容，不应该出现在角色筛选里。",
          category: "npc",
          source: "content",
          sourcePath: "Mods/Example/Characters/Dialogue/Sebastian.json",
        },
        {
          raw: "Hovsep",
          zh: "霍夫塞普",
          category: "npc",
          source: "content",
          sourcePath: "Mods/Example/Data/Characters/Hovsep.json",
        },
      ],
    };

    expect(listKnownCharactersFromCatalog(catalog)).toEqual(
      expect.arrayContaining(["Sebastian", "Hovsep"]),
    );
    expect(listKnownCharactersFromCatalog(catalog)).not.toContain("Mon");

    const options = getAvailableFilterOptions([], { translationCatalog: catalog });
    expect(options.npcNames).not.toContain("Mon");
    expect(formatNpcFilterLabel("Sebastian")).toBe("塞巴斯蒂安");
    expect(formatNpcFilterLabel("Mon")).toBe("Mon");
  });

  it("lists all vanilla NPCs even when they do not appear in current nodes", () => {
    const options = getAvailableFilterOptions([], {
      translationCatalog: null,
      runtimeState: null,
    });

    expect(options.npcNames).toContain("Sebastian");
    expect(options.npcNames).toContain("Shane");
    expect(options.npcNames).toContain("Sam");
    expect(options.npcNames.length).toBeGreaterThan(30);
  });
});
