import { beforeEach, describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { StoryNodeDetail } from "../StoryNodeDetail";
import { loadTranslationCatalog } from "../../lib/translations";
import type { StoryNodeEvaluation } from "../../types/story";

describe("StoryNodeDetail", () => {
  beforeEach(() => {
    loadTranslationCatalog({
      entries: [
        {
          category: "location",
          raw: "FarmHouse",
          zh: "农舍",
          source: "vanilla-export",
        },
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
  });

  it("keeps normal UI Chinese-first and moves raw/source into debug", () => {
    const node: StoryNodeEvaluation = {
      nodeId: "node-1",
      eventId: "2001",
      sourceModId: "example.mod",
      sourceModName: "Example Mod",
      location: "FarmHouse",
      rawKey: "2001/O Sebastian",
      rawPreconditions: ["O Sebastian", "f Pelette 3734"],
      rawScriptPreview: "speak Sebastian \"hello\"",
      status: "Locked",
      statusReason: "Progression conditions failed: missing event 11451861",
      conditionResult: {
        atomResults: [
          {
            raw: "f Pelette 3734",
            atomType: "Friendship",
            passed: false,
            reason: "friendship too low",
          },
        ],
      },
    };

    const html = renderToStaticMarkup(
      <StoryNodeDetail
        node={node}
        runtimeState={{
          year: 1,
          season: "spring",
          dayOfMonth: 5,
          dayOfWeek: "Monday",
          time: 900,
          weather: "sunny",
          currentLocation: "Town",
          playerName: "Farmer",
          friendshipPoints: { Pelette: 2000 },
          seenEvents: [],
          mail: [],
          dialogueAnswers: [],
        }}
      />,
    );

    expect(html).toContain("农舍");
    expect(html).toContain("原始数据 Debug");
    expect(html).toContain("Location source");
    expect(html).toContain("Condition raw：O Sebastian");
    expect(html).toContain("Source path：i18n/zh-CN.json");
    expect(html).not.toContain("conditionRaw");
    expect(html).not.toContain("reasonRaw");
    expect(html).not.toContain("未翻译：");
  });
});
