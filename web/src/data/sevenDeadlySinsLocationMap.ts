export type LocationConfidence = "high" | "medium" | "low";

export interface FallbackLocationEntry {
  zh: string;
  en: string;
  source: "fallback";
  confidence: LocationConfidence;
  note?: string;
}

export const sevenDeadlySinsLocationMap: Record<string, FallbackLocationEntry> = {
  Custom_Woods1: {
    zh: "七宗罪森林 1",
    en: "Custom Woods 1",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Woods2: {
    zh: "七宗罪森林 2",
    en: "Custom Woods 2",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Woods: {
    zh: "七宗罪森林",
    en: "Custom Woods",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Forest: {
    zh: "七宗罪森林",
    en: "Custom Forest",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Cave: {
    zh: "七宗罪洞穴",
    en: "Custom Cave",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_House: {
    zh: "七宗罪房屋",
    en: "Custom House",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Town: {
    zh: "七宗罪城镇",
    en: "Custom Town",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Beach: {
    zh: "七宗罪海滩",
    en: "Custom Beach",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Mountain: {
    zh: "七宗罪山区",
    en: "Custom Mountain",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
  Custom_Shop: {
    zh: "七宗罪商店",
    en: "Custom Shop",
    source: "fallback",
    confidence: "low",
    note: "临时翻译，需从 Mod 文件确认。",
  },
};
