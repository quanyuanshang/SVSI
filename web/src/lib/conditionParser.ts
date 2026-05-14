import {
  formatSeasonZh,
  formatWeatherZh,
  translateCharacter,
} from "./translations";
import { formatTimeRangeZh } from "./format";

export type ConditionType =
  | "dating"
  | "friendship"
  | "seenEvent"
  | "notSeenEvent"
  | "weather"
  | "notWeather"
  | "time"
  | "season"
  | "year"
  | "dayOfMonth"
  | "dayOfWeek"
  | "spouse"
  | "notSpouse"
  | "mail"
  | "notMail"
  | "hostMail"
  | "notHostMail"
  | "hostOrLocalMail"
  | "notHostOrLocalMail"
  | "dialogueAnswer"
  | "activeDialogueEvent"
  | "notActiveDialogueEvent"
  | "random"
  | "isHost"
  | "gender"
  | "daysPlayed"
  | "communityCenter"
  | "notCommunityCenter"
  | "npcVisibleHere"
  | "npcVisible"
  | "festivalDay"
  | "notFestivalDay"
  | "worldState"
  | "tile"
  | "reachedMineBottom"
  | "spouseBed"
  | "freeInventorySlots"
  | "gameStateQuery"
  | "missingPet"
  | "hasItem"
  | "jojaBundlesDone"
  | "inUpgradedHouse"
  | "earnedMoney"
  | "hasMoney"
  | "goldenWalnuts"
  | "roommate"
  | "notRoommate"
  | "sawSecretNote"
  | "upcomingFestival"
  | "notUpcomingFestival"
  | "unknown";

export interface TimeRange {
  start: number;
  end: number;
}

export interface ParsedCondition {
  raw: string;
  type: ConditionType;
  negated: boolean;
  target?: string;
  operator?: ">=" | "==" | "in";
  value?: string | number | string[] | TimeRange;
  descriptionZh: string;
  descriptionRaw?: string;
  unknownReason?: string;
}

const POSITIVE_ALIAS: Record<string, string> = {
  "*": "WorldState",
  "*n": "HostOrLocalMail",
  a: "Tile",
  b: "ReachedMineBottom",
  B: "SpouseBed",
  C: "CommunityCenterOrWarehouseDone",
  c: "FreeInventorySlots",
  D: "Dating",
  e: "SawEvent",
  f: "Friendship",
  G: "GameStateQuery",
  g: "Gender",
  H: "IsHost",
  h: "MissingPet",
  Hn: "HostMail",
  i: "HasItem",
  j: "DaysPlayed",
  J: "JojaBundlesDone",
  L: "InUpgradedHouse",
  m: "EarnedMoney",
  M: "HasMoney",
  N: "GoldenWalnuts",
  n: "LocalMail",
  O: "Spouse",
  p: "NpcVisibleHere",
  q: "ChoseDialogueAnswers",
  r: "Random",
  R: "Roommate",
  S: "SawSecretNote",
  s: "Season",
  t: "Time",
  u: "DayOfMonth",
  v: "NPCVisible",
  w: "Weather",
  y: "Year",
};

const NEGATIVE_ALIAS: Record<string, string> = {
  k: "SawEvent",
  o: "Spouse",
  d: "DayOfWeek",
  F: "FestivalDay",
  l: "LocalMail",
  Hl: "HostMail",
  "*l": "HostOrLocalMail",
  Rf: "Roommate",
  z: "Season",
  U: "UpcomingFestival",
  A: "ActiveDialogueEvent",
  X: "CommunityCenterOrWarehouseDone",
};

function tokenize(raw: string): string[] {
  return raw
    .trim()
    .split(/\s+/)
    .filter((token) => token.length > 0);
}

function normalizeKeyword(keyword: string): {
  canonical: string | null;
  legacyNegated: boolean;
} {
  if (keyword in POSITIVE_ALIAS) {
    return { canonical: POSITIVE_ALIAS[keyword], legacyNegated: false };
  }

  if (keyword in NEGATIVE_ALIAS) {
    return { canonical: NEGATIVE_ALIAS[keyword], legacyNegated: true };
  }

  switch (keyword.toLowerCase()) {
    case "activedialogueevent":
      return { canonical: "ActiveDialogueEvent", legacyNegated: false };
    case "communitycenterorwarehousedone":
      return { canonical: "CommunityCenterOrWarehouseDone", legacyNegated: false };
    case "season":
      return { canonical: "Season", legacyNegated: false };
    case "dayofmonth":
      return { canonical: "DayOfMonth", legacyNegated: false };
    case "dayofweek":
      return { canonical: "DayOfWeek", legacyNegated: false };
    case "festivalday":
      return { canonical: "FestivalDay", legacyNegated: false };
    case "freeinventoryslots":
      return { canonical: "FreeInventorySlots", legacyNegated: false };
    case "dating":
      return { canonical: "Dating", legacyNegated: false };
    case "time":
      return { canonical: "Time", legacyNegated: false };
    case "weather":
      return { canonical: "Weather", legacyNegated: false };
    case "friendship":
      return { canonical: "Friendship", legacyNegated: false };
    case "sawevent":
      return { canonical: "SawEvent", legacyNegated: false };
    case "gamestatequery":
      return { canonical: "GameStateQuery", legacyNegated: false };
    case "localmail":
      return { canonical: "LocalMail", legacyNegated: false };
    case "hostmail":
      return { canonical: "HostMail", legacyNegated: false };
    case "hostorlocalmail":
      return { canonical: "HostOrLocalMail", legacyNegated: false };
    case "chosedialogueanswers":
      return { canonical: "ChoseDialogueAnswers", legacyNegated: false };
    case "inupgradedhouse":
      return { canonical: "InUpgradedHouse", legacyNegated: false };
    case "jojabundlesdone":
      return { canonical: "JojaBundlesDone", legacyNegated: false };
    case "goldenwalnuts":
      return { canonical: "GoldenWalnuts", legacyNegated: false };
    case "earnedmoney":
      return { canonical: "EarnedMoney", legacyNegated: false };
    case "hasmoney":
      return { canonical: "HasMoney", legacyNegated: false };
    case "missingpet":
      return { canonical: "MissingPet", legacyNegated: false };
    case "hasitem":
      return { canonical: "HasItem", legacyNegated: false };
    case "spouse":
      return { canonical: "Spouse", legacyNegated: false };
    case "spousebed":
      return { canonical: "SpouseBed", legacyNegated: false };
    case "year":
      return { canonical: "Year", legacyNegated: false };
    case "daysplayed":
      return { canonical: "DaysPlayed", legacyNegated: false };
    case "gender":
      return { canonical: "Gender", legacyNegated: false };
    case "npcvisiblehere":
      return { canonical: "NpcVisibleHere", legacyNegated: false };
    case "npcvisible":
      return { canonical: "NPCVisible", legacyNegated: false };
    case "ishost":
      return { canonical: "IsHost", legacyNegated: false };
    case "roommate":
      return { canonical: "Roommate", legacyNegated: false };
    case "random":
      return { canonical: "Random", legacyNegated: false };
    case "reachedminebottom":
      return { canonical: "ReachedMineBottom", legacyNegated: false };
    case "sawsecretnote":
      return { canonical: "SawSecretNote", legacyNegated: false };
    case "tile":
      return { canonical: "Tile", legacyNegated: false };
    case "upcomingfestival":
      return { canonical: "UpcomingFestival", legacyNegated: false };
    case "worldstate":
      return { canonical: "WorldState", legacyNegated: false };
    default:
      return { canonical: null, legacyNegated: false };
  }
}

function npcName(target: string): string {
  return translateCharacter(target).zh;
}

function unknownCondition(raw: string, reason: string): ParsedCondition {
  return {
    raw,
    type: "unknown",
    negated: false,
    descriptionZh: `未解析条件：${raw}`,
    descriptionRaw: raw,
    unknownReason: reason,
  };
}

function buildSimpleCondition(
  raw: string,
  type: ConditionType,
  negated: boolean,
  descriptionZh: string,
  target?: string,
  value?: string | number | string[] | TimeRange,
  operator?: ">=" | "==" | "in",
): ParsedCondition {
  return {
    raw,
    type,
    negated,
    target,
    value,
    operator,
    descriptionZh,
    descriptionRaw: raw,
  };
}

export function formatConditionZh(condition: ParsedCondition): string {
  return condition.descriptionZh || `未解析条件：${condition.raw}`;
}

export function parseConditionFragment(rawFragment: string): ParsedCondition {
  const raw = rawFragment.trim();
  if (!raw) {
    return unknownCondition(raw, "空白条件");
  }

  let working = raw;
  let explicitNegated = false;

  if (working.startsWith("!")) {
    explicitNegated = true;
    working = working.slice(1).trim();
    if (!working) {
      return unknownCondition(raw, "缺少否定目标");
    }
  }

  const tokens = tokenize(working);
  if (tokens.length === 0) {
    return unknownCondition(raw, "无法切分条件参数");
  }

  const head = tokens[0];
  const args = tokens.slice(1);
  const { canonical, legacyNegated } = normalizeKeyword(head);
  if (!canonical) {
    return unknownCondition(raw, `未支持的条件 token：${head}`);
  }

  const negated = explicitNegated || legacyNegated;

  switch (canonical) {
    case "Dating":
      return parseDating(raw, args, negated);
    case "Friendship":
      return parseFriendship(raw, args, negated);
    case "SawEvent":
      return parseSeenEvent(raw, args, negated);
    case "Weather":
      return parseWeather(raw, args, negated);
    case "Time":
      return parseTime(raw, args, negated);
    case "Season":
      return parseSeason(raw, args, negated);
    case "Year":
      return parseYear(raw, args, negated);
    case "DayOfMonth":
      return parseDayOfMonth(raw, args, negated);
    case "DayOfWeek":
      return parseDayOfWeek(raw, args, negated);
    case "Spouse":
      return parseSpouse(raw, args, negated);
    case "LocalMail":
      return parseMail(raw, args, negated, "mail", "notMail", "本地邮件");
    case "HostMail":
      return parseMail(raw, args, negated, "hostMail", "notHostMail", "房主邮件");
    case "HostOrLocalMail":
      return parseMail(
        raw,
        args,
        negated,
        "hostOrLocalMail",
        "notHostOrLocalMail",
        "房主或本地邮件",
      );
    case "ChoseDialogueAnswers":
      return parseDialogueAnswer(raw, args, negated);
    case "ActiveDialogueEvent":
      return parseActiveDialogueEvent(raw, args, negated);
    case "FestivalDay":
      return buildSimpleCondition(
        raw,
        negated ? "notFestivalDay" : "festivalDay",
        negated,
        negated ? "今天不是节日" : "今天是节日",
      );
    case "IsHost":
      return buildSimpleCondition(raw, "isHost", negated, negated ? "玩家不是房主" : "玩家是房主");
    case "DaysPlayed":
      return parseDaysPlayed(raw, args, negated);
    case "Gender":
      return parseGender(raw, args, negated);
    case "CommunityCenterOrWarehouseDone":
      return buildSimpleCondition(
        raw,
        negated ? "notCommunityCenter" : "communityCenter",
        negated,
        negated ? "社区中心或 Joja 仓库尚未完成" : "社区中心或 Joja 仓库已完成",
      );
    case "NpcVisibleHere":
      return parseNpcVisible(raw, args, negated, "npcVisibleHere");
    case "NPCVisible":
      return parseNpcVisible(raw, args, negated, "npcVisible");
    case "Random":
      return buildSimpleCondition(
        raw,
        "random",
        negated,
        args[0] ? `随机概率条件：${args[0]}` : "随机概率条件",
        undefined,
        args[0],
      );
    case "WorldState":
      return buildSimpleCondition(raw, "worldState", negated, `世界状态：${args.join(" ") || "已保留原文"}`, undefined, args);
    case "Tile":
      return buildSimpleCondition(raw, "tile", negated, `图块条件：${args.join(" ") || "已保留原文"}`, undefined, args);
    case "ReachedMineBottom":
      return buildSimpleCondition(raw, "reachedMineBottom", negated, negated ? "尚未到达矿井底层" : "已到达矿井底层");
    case "SpouseBed":
      return buildSimpleCondition(raw, "spouseBed", negated, negated ? "家中没有可用的配偶床位" : "家中有可用的配偶床位");
    case "FreeInventorySlots":
      return buildSimpleCondition(raw, "freeInventorySlots", negated, args[0] ? `背包空位至少 ${args[0]} 格` : "背包空位条件", undefined, args[0], ">=");
    case "GameStateQuery":
      return buildSimpleCondition(raw, "gameStateQuery", negated, `游戏状态查询：${args.join(" ") || "已保留原文"}`, undefined, args);
    case "MissingPet":
      return buildSimpleCondition(raw, "missingPet", negated, negated ? "宠物未丢失" : "宠物处于丢失状态");
    case "HasItem":
      return buildSimpleCondition(raw, "hasItem", negated, `持有物品：${args.join(" ") || "已保留原文"}`, undefined, args);
    case "JojaBundlesDone":
      return buildSimpleCondition(raw, "jojaBundlesDone", negated, negated ? "Joja 收集包未完成" : "Joja 收集包已完成");
    case "InUpgradedHouse":
      return buildSimpleCondition(raw, "inUpgradedHouse", negated, negated ? "当前不在升级后的农舍或小屋中" : "当前在升级后的农舍或小屋中");
    case "EarnedMoney":
      return buildSimpleCondition(raw, "earnedMoney", negated, args[0] ? `累计收入至少 ${args[0]}` : "累计收入条件", undefined, args[0], ">=");
    case "HasMoney":
      return buildSimpleCondition(raw, "hasMoney", negated, args[0] ? `当前持有金币至少 ${args[0]}` : "金币条件", undefined, args[0], ">=");
    case "GoldenWalnuts":
      return buildSimpleCondition(raw, "goldenWalnuts", negated, args[0] ? `金核桃至少 ${args[0]}` : "金核桃条件", undefined, args[0], ">=");
    case "Roommate":
      return parseRoommate(raw, args, negated);
    case "SawSecretNote":
      return buildSimpleCondition(raw, "sawSecretNote", negated, args[0] ? `已看过秘密纸条 ${args[0]}` : "已看过秘密纸条", undefined, args[0]);
    case "UpcomingFestival":
      return buildSimpleCondition(raw, negated ? "notUpcomingFestival" : "upcomingFestival", negated, negated ? "今天不是即将到来的节日前夕" : "今天是即将到来的节日前夕");
    default:
      return unknownCondition(raw, `未实现的条件类型：${canonical}`);
  }
}

function parseDating(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少角色名");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    "dating",
    negated,
    negated ? `当前没有和${npcName(target)}约会` : `正在和${npcName(target)}约会`,
    target,
  );
}

function parseFriendship(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length < 2) {
    return unknownCondition(raw, "好感度参数不足");
  }

  const target = args[0];
  const numeric = Number.parseInt(args[1], 10);
  if (Number.isNaN(numeric)) {
    return unknownCondition(raw, `好感度阈值不是数字：${args[1]}`);
  }

  return buildSimpleCondition(
    raw,
    "friendship",
    negated,
    negated
      ? `${npcName(target)}好感度低于 ${numeric}`
      : `${npcName(target)}好感度至少 ${numeric}`,
    target,
    numeric,
    ">=",
  );
}

function parseSeenEvent(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少事件 ID");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    negated ? "notSeenEvent" : "seenEvent",
    negated,
    negated ? `未触发事件 ${target}` : `已触发事件 ${target}`,
    target,
  );
}

function parseWeather(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少天气参数");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    negated ? "notWeather" : "weather",
    negated,
    negated ? `非${formatWeatherZh(target)}` : `${formatWeatherZh(target)}`,
    target,
  );
}

function parseTime(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length < 2) {
    return unknownCondition(raw, "时间范围参数不足");
  }

  const start = Number.parseInt(args[0], 10);
  const end = Number.parseInt(args[1], 10);
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return unknownCondition(raw, "时间参数不是数字");
  }

  return buildSimpleCondition(
    raw,
    "time",
    negated,
    negated
      ? `时间不在 ${formatTimeRangeZh(start, end)} 之间`
      : `时间在 ${formatTimeRangeZh(start, end)} 之间`,
    undefined,
    { start, end },
    "in",
  );
}

function parseSeason(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少季节参数");
  }

  const label = args.map((season) => formatSeasonZh(season)).join(" / ");
  return buildSimpleCondition(
    raw,
    "season",
    negated,
    negated ? `不是 ${label}` : `${label}`,
    undefined,
    args,
  );
}

function parseYear(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少年份参数");
  }

  const numeric = Number.parseInt(args[0], 10);
  if (Number.isNaN(numeric)) {
    return unknownCondition(raw, "年份参数不是数字");
  }

  return buildSimpleCondition(
    raw,
    "year",
    negated,
    negated ? `年份早于第 ${numeric} 年` : `第 ${numeric} 年或之后`,
    undefined,
    numeric,
    ">=",
  );
}

function parseDayOfMonth(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少日期参数");
  }

  const label = args.join(" / ");
  return buildSimpleCondition(
    raw,
    "dayOfMonth",
    negated,
    negated ? `日期不是 ${label}` : `日期为 ${label}`,
    undefined,
    args,
  );
}

function parseDayOfWeek(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少星期参数");
  }

  const label = args.join(" / ");
  return buildSimpleCondition(
    raw,
    "dayOfWeek",
    negated,
    negated ? `今天不是 ${label}` : `今天是 ${label}`,
    undefined,
    args,
  );
}

function parseSpouse(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少角色名");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    negated ? "notSpouse" : "spouse",
    negated,
    negated
      ? `玩家没有和${npcName(target)}结婚或订婚`
      : `玩家已和${npcName(target)}结婚或订婚`,
    target,
  );
}

function parseMail(
  raw: string,
  args: string[],
  negated: boolean,
  positiveType: ConditionType,
  negativeType: ConditionType,
  label: string,
): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, `缺少${label}标记`);
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    negated ? negativeType : positiveType,
    negated,
    negated ? `未拥有${label} ${target}` : `已拥有${label} ${target}`,
    target,
  );
}

function parseDialogueAnswer(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少对话答案 ID");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    "dialogueAnswer",
    negated,
    negated ? `未选择对话答案 ${target}` : `已选择对话答案 ${target}`,
    target,
  );
}

function parseActiveDialogueEvent(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少对话主题");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    negated ? "notActiveDialogueEvent" : "activeDialogueEvent",
    negated,
    negated ? `当前没有活跃对话主题 ${target}` : `当前存在活跃对话主题 ${target}`,
    target,
  );
}

function parseDaysPlayed(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少天数参数");
  }

  const numeric = Number.parseInt(args[0], 10);
  if (Number.isNaN(numeric)) {
    return unknownCondition(raw, "天数参数不是数字");
  }

  return buildSimpleCondition(
    raw,
    "daysPlayed",
    negated,
    negated ? `游玩天数少于 ${numeric}` : `已游玩至少 ${numeric} 天`,
    undefined,
    numeric,
    ">=",
  );
}

function parseGender(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少性别参数");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    "gender",
    negated,
    negated ? `性别不是 ${target}` : `性别为 ${target}`,
    target,
  );
}

function parseNpcVisible(
  raw: string,
  args: string[],
  negated: boolean,
  type: "npcVisibleHere" | "npcVisible",
): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少角色名");
  }

  const target = args[0];
  const description =
    type === "npcVisibleHere"
      ? `${npcName(target)}当前在该地点且可见`
      : `${npcName(target)}当前可见`;

  return buildSimpleCondition(
    raw,
    type,
    negated,
    negated ? `不满足：${description}` : description,
    target,
  );
}

function parseRoommate(raw: string, args: string[], negated: boolean): ParsedCondition {
  if (args.length === 0) {
    return unknownCondition(raw, "缺少角色名");
  }

  const target = args[0];
  return buildSimpleCondition(
    raw,
    negated ? "notRoommate" : "roommate",
    negated,
    negated ? `玩家没有和${npcName(target)}成为室友` : `玩家已和${npcName(target)}成为室友`,
    target,
  );
}

export function parseConditions(rawFragments: readonly string[]): ParsedCondition[] {
  return rawFragments
    .map((fragment) => parseConditionFragment(fragment))
    .filter((parsed) => parsed.raw.length > 0);
}
