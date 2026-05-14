import { beforeEach, describe, expect, it } from "vitest";
import { applyStoryFilters, getAvailableFilterOptions } from "../storyFilters";
import { loadTranslationCatalog } from "../translations";
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
    searchText: "",
    ...overrides,
  };
}

describe("storyFilters - de-duplication by translated label", () => {
  beforeEach(() => {
    loadTranslationCatalog(null);
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
    const nodes = [
      makeNode({
        relatedDialogueRefs: [{ npcName: "Shane" }],
      }),
      makeNode({
        relatedDialogueRefs: [{ npcName: "shane" }],
      }),
    ];

    const options = getAvailableFilterOptions(nodes);
    expect(options.npcNames).toHaveLength(1);
    const rep = options.npcNames[0];
    const equivalents = options.npcEquivalents?.get(rep);
    expect(equivalents?.size).toBe(2);
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
});
