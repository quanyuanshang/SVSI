import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { LocationTranslationDebugPanel } from "../LocationTranslationDebugPanel";

describe("LocationTranslationDebugPanel", () => {
  it("shows raw, source and confidence for fallback and resource-backed locations", () => {
    const html = renderToStaticMarkup(
      <LocationTranslationDebugPanel
        translationCatalog={{
          entries: [
            {
              category: "location",
              raw: "Custom_Woods2",
              zh: "隐秘森林",
              source: "mod-i18n",
              sourceModName: "Seven Deadly Sins",
              sourcePath: "i18n/default.json",
            },
          ],
        }}
      />,
    );

    expect(html).toContain("地点映射 Debug 列表");
    expect(html).toContain("Custom_Woods2");
    expect(html).toContain("隐秘森林");
    expect(html).toContain("mod-i18n");
    expect(html).toContain("high");
    expect(html).toContain("Custom_Woods1");
    expect(html).toContain("七宗罪森林 1");
    expect(html).toContain("fallback");
    expect(html).toContain("low");
  });
});
