import { sevenDeadlySinsLocationMap, type FallbackLocationEntry } from "../data/sevenDeadlySinsLocationMap";
import type { TranslationCatalog, TranslationEntry } from "../types/story";

export type TranslationNamespace =
  | "location"
  | "character"
  | "weather"
  | "season"
  | "condition"
  | "conditionType"
  | "npc"
  | "item";

export interface Translation {
  zh: string;
  raw: string;
  untranslated: boolean;
  source: string;
  en?: string;
  confidence?: "high" | "medium" | "low";
  note?: string;
  sourceModId?: string;
  sourceModName?: string;
  sourcePath?: string;
}

interface UntranslatedName {
  raw: string;
  category: string;
  sourceMod?: string;
}

export interface LocationDebugRow {
  raw: string;
  zh: string;
  en: string;
  sourceMod: string;
  sourceFile: string;
  sourceType: string;
  confidence: "high" | "medium" | "low";
  note?: string;
}

const FALLBACK_LOCATIONS: Record<string, string> = {
  FarmHouse: "农舍",
  Town: "鹈鹕镇",
  Farm: "农场",
  Forest: "煤矿森林",
  Woods: "秘密森林",
  Beach: "海滩",
  Mountain: "山区",
  Railroad: "铁路",
  Mine: "矿井",
  Mines: "矿井",
  Desert: "沙漠",
  IslandSouth: "姜岛南部",
  IslandNorth: "姜岛北部",
  IslandWest: "姜岛西部",
  IslandEast: "姜岛东部",
  Caldera: "火山口",
  Saloon: "星之果实小酒馆",
  HaleyHouse: "海莉家",
  SamHouse: "山姆家",
  LeahHouse: "莉亚小屋",
  SandyHouse: "桑迪屋",
  SebastianRoom: "塞巴斯蒂安房间",
  SeedShop: "皮埃尔杂货店",
  AnimalShop: "玛妮牧场",
  Hospital: "诊所",
  ScienceHouse: "罗宾家",
  JoshHouse: "亚历克斯家",
  ElliottHouse: "艾利欧特小屋",
  HarveyRoom: "哈维房间",
  Trailer: "潘姆拖车",
  Trailer_Big: "潘姆新家",
  ManorHouse: "市长宅邸",
  WizardHouse: "法师塔",
  WizardHouseBasement: "法师塔地下室",
  CommunityCenter: "社区中心",
  Sewer: "下水道",
  BusStop: "巴士站",
  FishShop: "威利渔具店",
  ArchaeologyHouse: "博物馆",
  Backwoods: "农场后山",
  AdventureGuild: "冒险者公会",
  Blacksmith: "铁匠铺",
  JojaMart: "Joja 超市",
  MovieTheater: "电影院",
  BathHouse_Entry: "浴场入口",
  BathHouse_Pool: "浴场池",
  BathHouse_MensLocker: "男更衣室",
  BathHouse_WomensLocker: "女更衣室",
  Cabin: "小屋",
  Club: "撒漠俱乐部",
  Summit: "高峰",
  Temp: "临时场景",
  WitchSwamp: "女巫沼泽",
  Custom_GrampletonCoast: "格兰普顿海岸",
  Custom_GrampletonFields: "格兰普顿平原",
  Custom_GrampletonFields_Small: "格兰普顿平原",
  Custom_GrampletonFields_small: "格兰普顿平原",
  Custom_GrampletonSuburbs: "格兰普顿郊区",
  Custom_GrampletonSuburbsOutskirts: "格兰普顿郊区外围",
  Custom_GrampletonTrainStation: "格兰普顿火车站",
  Custom_GrampletonSuburbsTrainStation: "格兰普顿郊区火车站",
  Custom_MarnieShed: "玛妮的小屋",
  Custom_FirstSlashGuestRoom: "First Slash 客房",
  Custom_Woods1: sevenDeadlySinsLocationMap.Custom_Woods1.zh,
  Custom_Woods2: sevenDeadlySinsLocationMap.Custom_Woods2.zh,
  Custom_Woods: sevenDeadlySinsLocationMap.Custom_Woods.zh,
  Custom_Forest: sevenDeadlySinsLocationMap.Custom_Forest.zh,
  Custom_Cave: sevenDeadlySinsLocationMap.Custom_Cave.zh,
  Custom_House: sevenDeadlySinsLocationMap.Custom_House.zh,
  Custom_Town: sevenDeadlySinsLocationMap.Custom_Town.zh,
  Custom_Beach: sevenDeadlySinsLocationMap.Custom_Beach.zh,
  Custom_Mountain: sevenDeadlySinsLocationMap.Custom_Mountain.zh,
  Custom_Shop: sevenDeadlySinsLocationMap.Custom_Shop.zh,
};

const FALLBACK_CHARACTERS: Record<string, string> = {
  Sam: "山姆",
  Abigail: "阿比盖尔",
  Sebastian: "塞巴斯蒂安",
  Penny: "潘妮",
  Haley: "海莉",
  Leah: "莉亚",
  Maru: "玛鲁",
  Alex: "亚历克斯",
  Elliott: "艾利欧特",
  Shane: "谢恩",
  Harvey: "哈维",
  Emily: "艾米丽",
  Wizard: "法师",
  Lewis: "刘易斯",
  Marnie: "玛妮",
  Robin: "罗宾",
  Demetrius: "迪米特里",
  Linus: "莱纳斯",
  Pierre: "皮埃尔",
  Caroline: "卡洛琳",
  Jodi: "乔迪",
  Kent: "肯特",
  Vincent: "文森特",
  Jas: "贾斯",
  Pam: "潘姆",
  Gus: "格斯",
  Clint: "克林特",
  Willy: "威利",
  Evelyn: "艾芙琳",
  George: "乔治",
  Dwarf: "矮人",
  Krobus: "科罗布斯",
  Sandy: "桑迪",
  Leo: "雷欧",
  Pelette: "佩莱特",
  Uriel: "乌列",
  Gunther: "甘瑟",
  Morris: "莫里斯",
  Marlon: "马龙",
  Apples: "苹果",
  Bear: "熊",
  Peaches: "桃子",
  Andy: "安迪",
  Claire: "克莱尔",
  Lance: "兰斯",
  Olivia: "奥利维亚",
  Sophia: "索菲娅",
  Susan: "苏珊",
  Victor: "维克多",
  Scarlett: "斯嘉丽",
  Morgan: "摩根",
  Martin: "马丁",
  Wendy: "温蒂",
  GuntherSilvian: "甘瑟",
  MarlonFay: "马龙",
  MorrisTod: "莫里斯",
  SVE_Henchman: "帮手",
  Henchman: "帮手",
  Lance_Sword: "兰斯（剑）",
  Regla: "瑞格拉",
  Sariel: "白井"
};

const FALLBACK_WEATHER: Record<string, string> = {
  sunny: "晴天",
  rainy: "雨天",
  rain: "雨天",
  stormy: "雷暴",
  storm: "雷暴",
  snowy: "雪天",
  snow: "雪天",
  windy: "大风",
  wedding: "婚礼日",
  festival: "节日",
};

const FALLBACK_SEASONS: Record<string, string> = {
  spring: "春季",
  summer: "夏季",
  fall: "秋季",
  autumn: "秋季",
  winter: "冬季",
};

const FALLBACK_CONDITIONS: Record<string, string> = {
  dating: "约会",
  friendship: "好感度",
  seenEvent: "已触发事件",
  notSeenEvent: "未触发事件",
  weather: "天气",
  notWeather: "非该天气",
  time: "时间",
  season: "季节",
  year: "年份",
  dayOfMonth: "日期",
  dayOfWeek: "星期",
  spouse: "婚姻",
  notSpouse: "非配偶",
  mail: "本地邮件",
  notMail: "未持有本地邮件",
  hostMail: "房主邮件",
  notHostMail: "未持有房主邮件",
  hostOrLocalMail: "房主或本地邮件",
  notHostOrLocalMail: "未持有房主或本地邮件",
  dialogueAnswer: "对话选项",
  activeDialogueEvent: "活跃对话主题",
  notActiveDialogueEvent: "非活跃对话主题",
  random: "随机条件",
  isHost: "房主",
  gender: "性别",
  daysPlayed: "游玩天数",
  communityCenter: "社区中心或仓库已完成",
  notCommunityCenter: "社区中心或仓库未完成",
  npcVisibleHere: "角色在当前地点可见",
  npcVisible: "角色可见",
  festivalDay: "节日当天",
  notFestivalDay: "非节日",
  worldState: "世界状态",
  tile: "地图图块",
  reachedMineBottom: "已到达矿井底层",
  spouseBed: "配偶床位",
  freeInventorySlots: "背包空位",
  gameStateQuery: "游戏状态查询",
  missingPet: "宠物丢失",
  hasItem: "持有物品",
  jojaBundlesDone: "Joja 收集包完成",
  inUpgradedHouse: "升级后的农舍或小屋",
  earnedMoney: "累计收入",
  hasMoney: "持有金币",
  goldenWalnuts: "金核桃",
  roommate: "室友",
  notRoommate: "非室友",
  sawSecretNote: "已看过秘密纸条",
  upcomingFestival: "即将到来的节日",
  notUpcomingFestival: "不是即将到来的节日",
  unknown: "未解析条件",
};

let activeCatalog: TranslationCatalog | null = null;
const untranslatedNames = new Map<string, UntranslatedName>();

function normalizeKey(value?: string | null): string {
  return (value ?? "").trim().toLowerCase();
}

// Some Content Patcher targets carry literal CP tokens (e.g. `{{FarmHouse}}`,
// `{{ Summit }}`) because the index runs without token resolution. Strip the
// outer `{{ }}` (and trailing/leading whitespace inside) so the inner key can
// hit fallback / catalog lookups; we keep the raw shape for display callers.
function stripTemplateBraces(value: string): string {
  const match = value.match(/^\{\{\s*(.+?)\s*\}\}$/);
  return match ? match[1] : value;
}

function normalizeCategory(namespace: TranslationNamespace): string {
  switch (namespace) {
    case "character":
      return "npc";
    case "conditionType":
      return "condition";
    default:
      return namespace;
  }
}

function getFallbackMap(category: string): Record<string, string> {
  switch (category) {
    case "location":
      return FALLBACK_LOCATIONS;
    case "npc":
      return FALLBACK_CHARACTERS;
    case "weather":
      return FALLBACK_WEATHER;
    case "season":
      return FALLBACK_SEASONS;
    case "condition":
      return FALLBACK_CONDITIONS;
    default:
      return {};
  }
}

function findCatalogEntry(
  raw: string,
  category: string,
  sourceMod?: string | null,
): TranslationEntry | null {
  const rawKey = normalizeKey(raw);
  const categoryKey = normalizeKey(category);
  const sourceKey = normalizeKey(sourceMod);
  const entries = activeCatalog?.entries ?? [];

  if (sourceKey) {
    const scoped = entries.find(
      (entry) =>
        normalizeKey(entry.category) === categoryKey &&
        normalizeKey(entry.raw) === rawKey &&
        (normalizeKey(entry.sourceModId) === sourceKey ||
          normalizeKey(entry.sourceModName) === sourceKey),
    );
    if (scoped) {
      return scoped;
    }
  }

  return (
    entries.find(
      (entry) =>
        normalizeKey(entry.category) === categoryKey &&
        normalizeKey(entry.raw) === rawKey &&
        !entry.sourceModId,
    ) ??
    entries.find(
      (entry) =>
        normalizeKey(entry.category) === categoryKey &&
        normalizeKey(entry.raw) === rawKey,
    ) ??
    null
  );
}

function findFallbackValue(raw: string, category: string): string | null {
  const map = getFallbackMap(category);
  if (raw in map) {
    return map[raw];
  }

  const rawKey = normalizeKey(raw);
  for (const [key, value] of Object.entries(map)) {
    if (normalizeKey(key) === rawKey) {
      return value;
    }
  }

  return null;
}

function findFallbackLocationMetadata(raw: string): FallbackLocationEntry | null {
  if (raw in sevenDeadlySinsLocationMap) {
    return sevenDeadlySinsLocationMap[raw];
  }

  const rawKey = normalizeKey(raw);
  for (const [key, value] of Object.entries(sevenDeadlySinsLocationMap)) {
    if (normalizeKey(key) === rawKey) {
      return value;
    }
  }

  return null;
}

function prettifyRawName(raw: string): string {
  return raw
    .replace(/^Custom_/, "Custom ")
    .replace(/^Custom_SDS\./, "Custom SDS ")
    .replace(/[._]/g, " ")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
}

function rememberUntranslated(raw: string, category: string, sourceMod?: string | null): void {
  const key = `${normalizeKey(category)}|${normalizeKey(raw)}|${normalizeKey(sourceMod)}`;
  untranslatedNames.set(key, {
    raw,
    category,
    sourceMod: sourceMod ?? undefined,
  });
}

export function loadVanillaTranslations(): TranslationCatalog {
  return {
    entries: [
      ...Object.entries(FALLBACK_LOCATIONS).map(([raw, zh]) => ({
        category: "location",
        raw,
        zh,
        source: "vanilla-export",
      })),
      ...Object.entries(FALLBACK_CHARACTERS).map(([raw, zh]) => ({
        category: "npc",
        raw,
        zh,
        source: "vanilla-export",
      })),
    ],
  };
}

export function loadTranslationCatalog(catalog?: TranslationCatalog | null): void {
  activeCatalog = catalog ?? null;
  untranslatedNames.clear();
}

export function listKnownCharactersFromCatalog(
  catalog?: TranslationCatalog | null,
): string[] {
  const names = new Set<string>(Object.keys(FALLBACK_CHARACTERS));

  for (const entry of catalog?.entries ?? activeCatalog?.entries ?? []) {
    const raw = entry.raw?.trim() ?? "";
    if (normalizeKey(entry.category) !== "npc" || raw.length === 0) {
      continue;
    }

    if (isNpcCatalogEntry(raw) && isTrustedNpcCatalogSource(entry)) {
      names.add(raw);
    }
  }

  return Array.from(names).sort((left, right) => left.localeCompare(right, "en"));
}

export function formatNpcFilterLabel(raw: string): string {
  const trimmed = raw.trim();
  if (FALLBACK_CHARACTERS[trimmed]) {
    return FALLBACK_CHARACTERS[trimmed];
  }

  const caseMatch = Object.keys(FALLBACK_CHARACTERS).find(
    (name) => name.toLowerCase() === trimmed.toLowerCase(),
  );
  if (caseMatch) {
    return FALLBACK_CHARACTERS[caseMatch];
  }

  const translated = translateCharacter(trimmed);
  if (translated.untranslated || looksLikeDialogueLine(translated.zh)) {
    return trimmed;
  }

  return translated.zh;
}

function isTrustedNpcCatalogSource(entry: TranslationEntry): boolean {
  if (entry.source === "vanilla-export") {
    return true;
  }

  const path = (entry.sourcePath ?? "").replace(/\\/g, "/").toLowerCase();
  if (path.includes("dialogue")) {
    return false;
  }

  return (
    path.includes("data/characters") ||
    path.includes("data/npcdispositions") ||
    (path.includes("/characters/") && !path.includes("dialogue"))
  );
}

function looksLikeDialogueLine(zh: string): boolean {
  const trimmed = zh.trim();
  if (trimmed.length <= 12) {
    return false;
  }

  if (/[。！？…]/.test(trimmed)) {
    return true;
  }

  return trimmed.length >= 24;
}

function isNpcCatalogEntry(raw: string): boolean {
  if (raw.length < 2 || raw.length > 32) {
    return false;
  }

  if (!/^[A-Za-z][A-Za-z0-9_]*$/.test(raw)) {
    return false;
  }

  if (/^\d+$/.test(raw) || /\d{5,}/.test(raw)) {
    return false;
  }

  return true;
}

export function loadModTranslations(modPath: string): TranslationEntry[] {
  const modKey = normalizeKey(modPath);
  return (activeCatalog?.entries ?? []).filter(
    (entry) =>
      normalizeKey(entry.sourceModId) === modKey ||
      normalizeKey(entry.sourceModName) === modKey ||
      normalizeKey(entry.sourcePath) === modKey,
  );
}

export function getUntranslatedNames(): UntranslatedName[] {
  return Array.from(untranslatedNames.values()).sort((a, b) =>
    `${a.category}:${a.raw}`.localeCompare(`${b.category}:${b.raw}`, "zh-CN"),
  );
}

export function resolveDisplayName(
  raw?: string | null,
  category: TranslationNamespace = "condition",
  sourceMod?: string | null,
): Translation {
  const cleaned = (raw ?? "").trim();
  const normalizedCategory = normalizeCategory(category);

  if (!cleaned) {
    return {
      zh: "未知",
      raw: "",
      untranslated: true,
      source: "empty",
    };
  }

  const stripped = stripTemplateBraces(cleaned);
  const lookupKeys = cleaned === stripped ? [cleaned] : [cleaned, stripped];

  for (const key of lookupKeys) {
    const scoped = findCatalogEntry(key, normalizedCategory, sourceMod);
    if (scoped) {
      return {
        zh: scoped.zh,
        raw: cleaned,
        untranslated: false,
        source: scoped.source,
        en: prettifyRawName(stripped),
        confidence: scoped.source === "fallback" ? "low" : "high",
        sourceModId: scoped.sourceModId,
        sourceModName: scoped.sourceModName,
        sourcePath: scoped.sourcePath,
      };
    }
  }

  for (const key of lookupKeys) {
    const fallback = findFallbackValue(key, normalizedCategory);
    if (fallback) {
      const fallbackLocation =
        normalizedCategory === "location" ? findFallbackLocationMetadata(key) : null;

      return {
        zh: fallback,
        raw: cleaned,
        untranslated: false,
        source: "fallback",
        en: fallbackLocation?.en ?? prettifyRawName(stripped),
        confidence: fallbackLocation?.confidence ?? "low",
        note: fallbackLocation?.note,
      };
    }
  }

  rememberUntranslated(cleaned, normalizedCategory, sourceMod);
  return {
    zh: cleaned,
    raw: cleaned,
    untranslated: true,
    source: "raw",
    en: prettifyRawName(stripped),
  };
}

export function translate(key: string, namespace: TranslationNamespace): Translation {
  return resolveDisplayName(key, namespace);
}

export function translateLocation(raw?: string | null, sourceMod?: string | null): Translation {
  return resolveDisplayName(raw, "location", sourceMod);
}

export function translateCharacter(raw?: string | null, sourceMod?: string | null): Translation {
  return resolveDisplayName(raw, "npc", sourceMod);
}

export function translateWeather(raw?: string | null): Translation {
  return resolveDisplayName(raw, "weather");
}

export function translateSeason(raw?: string | null): Translation {
  return resolveDisplayName(raw, "season");
}

export function translateConditionType(raw?: string | null): Translation {
  return resolveDisplayName(raw, "condition");
}

export function translateName(
  raw: string,
  category: "npc" | "location" | "condition" | "weather" | "season" | "item" = "condition",
  sourceMod?: string | null,
): string {
  return resolveDisplayName(raw, category, sourceMod).zh;
}

export function formatTranslationPrimary(translation: Translation): string {
  return translation.zh;
}

export function formatTranslationSecondary(translation: Translation): string | null {
  if (!translation.raw || translation.raw === translation.zh) {
    return null;
  }

  return translation.raw;
}

export function formatSeasonZh(season?: string | null): string {
  return translateSeason(season).zh;
}

export function formatWeatherZh(weather?: string | null): string {
  return translateWeather(weather).zh;
}

export function isKnownCharacter(raw?: string | null): boolean {
  return !!raw && isNpcCatalogEntry(raw.trim()) && !translateCharacter(raw).untranslated;
}

export function listKnownCharacters(): string[] {
  return listKnownCharactersFromCatalog(activeCatalog);
}

function isRelevantLocationEntry(entry: TranslationEntry): boolean {
  const modLabel = `${entry.sourceModName ?? ""} ${entry.sourceModId ?? ""}`.toLowerCase();
  return (
    entry.category.toLowerCase() === "location" &&
    (modLabel.includes("seven deadly sins") ||
      modLabel.includes("stardew valley expanded") ||
      modLabel.includes("mh event list") ||
      entry.raw.startsWith("Custom_SDS.") ||
      entry.raw.startsWith("Custom_Woods") ||
      entry.raw.startsWith("Custom_Grampleton"))
  );
}

export function getLocationDebugRows(catalog?: TranslationCatalog | null): LocationDebugRow[] {
  const rows = new Map<string, LocationDebugRow>();
  const entries = (catalog ?? activeCatalog)?.entries ?? [];

  for (const entry of entries) {
    if (!isRelevantLocationEntry(entry)) {
      continue;
    }

    rows.set(`${entry.raw}|${entry.sourceModId ?? ""}`, {
      raw: entry.raw,
      zh: entry.zh,
      en: prettifyRawName(entry.raw),
      sourceMod: entry.sourceModName ?? entry.sourceModId ?? "未知 Mod",
      sourceFile: entry.sourcePath ?? "未记录",
      sourceType: entry.source,
      confidence: entry.source === "fallback" ? "low" : "high",
    });
  }

  for (const [raw, entry] of Object.entries(sevenDeadlySinsLocationMap)) {
    if (Array.from(rows.values()).some((row) => row.raw === raw)) {
      continue;
    }

    rows.set(`${raw}|fallback`, {
      raw,
      zh: entry.zh,
      en: entry.en,
      sourceMod: "Seven Deadly Sins",
      sourceFile: "src/data/sevenDeadlySinsLocationMap.ts",
      sourceType: entry.source,
      confidence: entry.confidence,
      note: entry.note,
    });
  }

  return Array.from(rows.values()).sort((left, right) =>
    left.raw.localeCompare(right.raw, "en"),
  );
}
