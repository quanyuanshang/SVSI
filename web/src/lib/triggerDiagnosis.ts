import {
  formatConditionZh,
  parseConditions,
  type ConditionType,
  type ParsedCondition,
  type TimeRange,
} from "./conditionParser";
import {
  formatLocationZh,
  formatSeasonZh,
  formatTimeRangeZh,
  formatWeatherZh,
} from "./format";
import { translateCharacter } from "./translations";
import type { RuntimeGameState, StoryNodeEvaluation } from "../types/story";

export interface CurrentGameState {
  year?: number;
  season?: string;
  day?: number;
  dayOfWeek?: string;
  time?: number;
  location?: string;
  weather?: string;
  isFestivalDay?: boolean | null;
  installedModIds?: string[];
  friendship?: Record<string, number>;
  dating?: string[];
  marriedTo?: string | null;
  spouse?: string | string[] | null;
  spouses?: string[];
  engagedTo?: string | string[] | null;
  roommate?: string | string[] | null;
  visibleNpcNamesHere?: string[];
  inUpgradedHouse?: boolean | null;
  spouseBedKnown?: boolean;
  hasSpouseBed?: boolean | null;
  seenEvents?: string[];
  mailFlags?: string[];
  conversationTopics?: string[];
}

export interface DiagnosticItem {
  conditionRaw: string;
  status: "satisfied" | "unsatisfied" | "unknown";
  type: ConditionType;
  negated: boolean;
  descriptionZh: string;
  descriptionRaw?: string;
  reasonZh: string;
  reasonRaw?: string;
  expectedZh?: string;
  actualZh?: string;
}

export interface DiagnosisResult {
  canTrigger: boolean;
  satisfied: DiagnosticItem[];
  unsatisfied: DiagnosticItem[];
  unknown: DiagnosticItem[];
}

export interface DiagnoseOptions {
  /**
   * Optional set of event IDs that are actually present in the indexed event
   * list. When provided, `e EventX` / `!e EventX` conditions whose target is
   * missing from this set get a note in their reason so the user understands
   * the prereq event was never defined (only referenced).
   */
  availableEventIds?: ReadonlySet<string>;
}

type EvalOutcome = "satisfied" | "unsatisfied" | "unknown";

interface AtomEvaluation {
  outcome: EvalOutcome;
  reasonZh: string;
  reasonRaw?: string;
  expectedZh?: string;
  actualZh?: string;
}

function npcZh(raw?: string): string {
  return translateCharacter(raw).zh;
}

function normalizeNameKey(value?: string | null): string {
  return (value ?? "").trim().toLowerCase();
}

function normalizeStringList(values?: readonly string[] | null): string[] {
  return (values ?? []).map((value) => value.trim()).filter((value) => value.length > 0);
}

function flattenNames(value: string | string[] | null | undefined): string[] {
  if (Array.isArray(value)) {
    return normalizeStringList(value);
  }

  if (typeof value === "string" && value.trim().length > 0) {
    return [value.trim()];
  }

  return [];
}

function getRelationshipNames(state: CurrentGameState): string[] {
  const names = new Map<string, string>();

  for (const name of [
    ...flattenNames(state.marriedTo),
    ...flattenNames(state.spouse),
    ...flattenNames(state.spouses),
    ...flattenNames(state.engagedTo),
    ...flattenNames(state.roommate),
  ]) {
    names.set(normalizeNameKey(name), name);
  }

  return Array.from(names.values());
}

function hasRelationshipWith(state: CurrentGameState, target?: string | null): boolean {
  const normalizedTarget = normalizeNameKey(target);
  if (!normalizedTarget) {
    return false;
  }

  return getRelationshipNames(state).some((name) => normalizeNameKey(name) === normalizedTarget);
}

function hasAnyRelationshipRuntimeData(state: CurrentGameState): boolean {
  return (
    state.marriedTo !== undefined ||
    state.spouse !== undefined ||
    state.spouses !== undefined ||
    state.engagedTo !== undefined ||
    state.roommate !== undefined ||
    state.dating !== undefined ||
    state.friendship !== undefined
  );
}

function relationshipStateValuesForNpc(state: CurrentGameState, npc?: string | null): string[] {
  const normalizedNpc = normalizeNameKey(npc);
  if (!normalizedNpc) {
    return [];
  }

  const values = new Set<string>();
  const hasName = (value: string | string[] | null | undefined) =>
    flattenNames(value).some((name) => normalizeNameKey(name) === normalizedNpc);

  if (hasName(state.engagedTo)) {
    values.add("Engaged");
  }

  if (hasName(state.marriedTo) || hasName(state.spouse) || hasName(state.spouses)) {
    values.add("Married");
  }

  if (hasName(state.roommate)) {
    values.add("Roommate");
  }

  if (state.dating?.some((name) => normalizeNameKey(name) === normalizedNpc)) {
    values.add("Dating");
  }

  if (state.friendship && Object.keys(state.friendship).some((name) => normalizeNameKey(name) === normalizedNpc)) {
    values.add("Friendly");
  }

  return Array.from(values);
}

function relationshipStateZh(value: string): string {
  switch (value.trim().toLowerCase()) {
    case "engaged":
      return "订婚";
    case "married":
      return "结婚";
    case "dating":
      return "约会";
    case "roommate":
      return "室友";
    case "friendly":
      return "好感";
    default:
      return value;
  }
}

function parseBooleanLike(value?: string | boolean | null): boolean {
  if (typeof value === "boolean") {
    return value;
  }

  const normalized = value?.trim().toLowerCase();
  return normalized !== "false" && normalized !== "no" && normalized !== "0";
}

function isTimeRange(value: unknown): value is TimeRange {
  return (
    !!value &&
    typeof value === "object" &&
    "start" in (value as Record<string, unknown>) &&
    "end" in (value as Record<string, unknown>)
  );
}

function asNumber(value: unknown): number | null {
  return typeof value === "number" && !Number.isNaN(value) ? value : null;
}

function unknownEvaluation(condition: ParsedCondition, reasonZh?: string): AtomEvaluation {
  return {
    outcome: "unknown",
    reasonZh:
      reasonZh ??
      `未解析条件：${condition.raw}。已保留原始条件，等待后续补充解析规则。`,
  };
}

function normalizeDayOfWeekToken(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return trimmed;
  }

  const upperMap: Record<string, string> = {
    MON: "Monday",
    TUE: "Tuesday",
    WED: "Wednesday",
    THU: "Thursday",
    FRI: "Friday",
    SAT: "Saturday",
    SUN: "Sunday",
    MONDAY: "Monday",
    TUESDAY: "Tuesday",
    WEDNESDAY: "Wednesday",
    THURSDAY: "Thursday",
    FRIDAY: "Friday",
    SATURDAY: "Saturday",
    SUNDAY: "Sunday",
  };

  const key = trimmed.toUpperCase();
  if (upperMap[key]) {
    return upperMap[key];
  }

  return trimmed[0].toUpperCase() + trimmed.slice(1).toLowerCase();
}

function evaluateDayOfWeek(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  const argList = Array.isArray(condition.value)
    ? condition.value.map((item) => String(item))
    : [];
  if (argList.length === 0) {
    return unknownEvaluation(condition, "缺少星期参数。");
  }

  if (!state.dayOfWeek?.trim()) {
    return unknownEvaluation(condition, "运行时未提供当前星期。");
  }

  const current = normalizeDayOfWeekToken(state.dayOfWeek);
  const candidates = argList.map(normalizeDayOfWeekToken);
  const inList = candidates.some((candidate) => candidate === current);

  if (condition.negated) {
    const matches = !inList;
    return {
      outcome: matches ? "satisfied" : "unsatisfied",
      reasonZh: matches
        ? `星期条件满足：今天（${current}）不在排除列表 [${candidates.join("、")}]。`
        : `星期条件不满足：今天（${current}）在排除列表 [${candidates.join("、")}] 中。`,
    };
  }

  const matches = inList;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `星期条件满足：今天（${current}）在要求的 [${candidates.join("、")}] 中。`
      : `星期条件不满足：今天（${current}）不在要求的 [${candidates.join("、")}] 中。`,
  };
}

function evaluateSpouseBed(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  if (state.spouseBedKnown !== true) {
    return unknownEvaluation(condition, "当前暂无法判断家中是否有可用配偶床位。");
  }

  const has = state.hasSpouseBed === true;
  const matches = condition.negated ? !has : has;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? condition.negated
        ? "配偶床位条件满足：当前没有可用的配偶床位（或等价判定）。"
        : "配偶床位条件满足：家中有可用的配偶床位。"
      : condition.negated
        ? "配偶床位条件不满足：仍检测到有配偶床位可用。"
        : "配偶床位条件不满足：家中没有可用的配偶床位。",
  };
}

function evaluateAtom(
  condition: ParsedCondition,
  state: CurrentGameState,
  options?: DiagnoseOptions,
): AtomEvaluation {
  if (condition.type === "npcVisibleHere") {
    return evaluateNpcVisibleHere(condition, state);
  }

  if (condition.type === "inUpgradedHouse") {
    return evaluateInUpgradedHouse(condition, state);
  }

  switch (condition.type) {
    case "dating":
      return evaluateDating(condition, state);
    case "friendship":
      return evaluateFriendship(condition, state);
    case "seenEvent":
    case "notSeenEvent":
      return evaluateSeenEvent(condition, state, options?.availableEventIds);
    case "weather":
    case "notWeather":
      return evaluateWeather(condition, state);
    case "time":
      return evaluateTime(condition, state);
    case "season":
      return evaluateSeason(condition, state);
    case "year":
      return evaluateYear(condition, state);
    case "spouse":
    case "notSpouse":
      return evaluateSpouse(condition, state);
    case "mail":
    case "notMail":
      return evaluateMail(condition, state, "本地邮件");
    case "activeDialogueEvent":
    case "notActiveDialogueEvent":
      return evaluateActiveDialogue(condition, state);
    case "dayOfMonth":
      return evaluateDayOfMonth(condition, state);
    case "seasonDay":
      return evaluateSeasonDay(condition, state);
    case "relationshipStates":
      return evaluateRelationshipStates(condition, state);
    case "festivalDay":
    case "notFestivalDay":
      return evaluateFestivalDay(condition, state);
    case "npcVisibleHere":
      return unknownEvaluation(condition, `当前暂无法判断：${npcZh(condition.target)}当前在该地点且可见。`);
    case "npcVisible":
      return unknownEvaluation(condition, `当前暂无法判断：${npcZh(condition.target)}当前可见。`);
    case "spouseBed":
      return evaluateSpouseBed(condition, state);
    case "dayOfWeek":
      return evaluateDayOfWeek(condition, state);
    case "hostMail":
    case "notHostMail":
    case "hostOrLocalMail":
    case "notHostOrLocalMail":
    case "isHost":
    case "gender":
    case "daysPlayed":
    case "communityCenter":
    case "notCommunityCenter":
    case "random":
    case "worldState":
    case "tile":
    case "reachedMineBottom":
    case "freeInventorySlots":
    case "gameStateQuery":
    case "missingPet":
    case "hasItem":
    case "jojaBundlesDone":
    case "inUpgradedHouse":
    case "earnedMoney":
    case "hasMoney":
    case "goldenWalnuts":
    case "roommate":
    case "notRoommate":
    case "sawSecretNote":
    case "upcomingFestival":
    case "notUpcomingFestival":
    case "dialogueAnswer":
    case "unknown":
      return unknownEvaluation(condition, condition.unknownReason);
    default:
      return unknownEvaluation(condition);
  }
}

function evaluateDating(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, "约会条件缺少角色名。");
  }

  if (!state.dating) {
    return unknownEvaluation(condition, "运行时未提供约会列表。");
  }

  const isDating = state.dating.some((name) => normalizeNameKey(name) === normalizeNameKey(condition.target));
  const matches = condition.negated ? !isDating : isDating;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? condition.negated
        ? `约会状态满足：当前没有和${npcZh(condition.target)}约会`
        : `约会状态满足：当前正在和${npcZh(condition.target)}约会`
      : condition.negated
        ? `约会不满足：需要没有和${npcZh(condition.target)}约会`
        : `约会不满足：需要正在和${npcZh(condition.target)}约会`,
  };
}

function evaluateNpcVisibleHere(
  condition: ParsedCondition,
  state: CurrentGameState,
): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, "角色可见条件缺少角色名。");
  }

  if (!state.visibleNpcNamesHere) {
    return unknownEvaluation(
      condition,
      `当前暂无法判断：${npcZh(condition.target)}当前在该地点且可见。`,
    );
  }

  const isVisibleHere = state.visibleNpcNamesHere.some(
    (name) => normalizeNameKey(name) === normalizeNameKey(condition.target),
  );
  const matches = condition.negated ? !isVisibleHere : isVisibleHere;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? condition.negated
        ? `角色可见条件满足：${npcZh(condition.target)}当前不在该地点或不可见`
        : `角色可见条件满足：${npcZh(condition.target)}当前在该地点且可见`
      : condition.negated
        ? `角色可见条件不满足：需要${npcZh(condition.target)}当前不在该地点或不可见`
        : `角色可见条件不满足：需要${npcZh(condition.target)}当前在该地点且可见`,
  };
}

function evaluateFriendship(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, "好感度条件缺少角色名。");
  }

  const threshold = asNumber(condition.value);
  if (threshold === null) {
    const rawThreshold = typeof condition.value === "string" ? condition.value : "";
    if (/MinFriendship/i.test(rawThreshold)) {
      return unknownEvaluation(
        condition,
        "外部/动态 token MinFriendship 未导出，无法判断好感度阈值。",
      );
    }
    return unknownEvaluation(condition, "好感度阈值无效。");
  }

  if (!state.friendship) {
    return unknownEvaluation(condition, "运行时未提供好感度数据。");
  }

  const actualValue = state.friendship[condition.target];
  if (typeof actualValue !== "number") {
    return unknownEvaluation(condition, `运行时未记录 ${npcZh(condition.target)} 的好感度。`);
  }

  const passed = actualValue >= threshold;
  const matches = condition.negated ? !passed : passed;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `好感度满足：${npcZh(condition.target)}好感度已达 ${actualValue}`
      : `好感度不满足：${npcZh(condition.target)}当前好感度为 ${actualValue}，需要至少 ${threshold}`,
    expectedZh: `${npcZh(condition.target)}好感度至少 ${threshold}`,
    actualZh: `${npcZh(condition.target)}好感度 ${actualValue}`,
  };
}

function evaluateSeenEvent(
  condition: ParsedCondition,
  state: CurrentGameState,
  availableEventIds?: ReadonlySet<string>,
): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, "事件条件缺少事件 ID。");
  }

  if (!state.seenEvents) {
    return unknownEvaluation(condition, "运行时未提供已触发事件列表。");
  }

  const target = condition.target;
  const seen = state.seenEvents.includes(target);
  const wantSeen = condition.type === "seenEvent";
  const matches = wantSeen ? seen : !seen;
  const indexed =
    availableEventIds === undefined || availableEventIds.has(target);
  const indexNote = indexed
    ? ""
    : `（注意：前置事件 ${target} 未在事件索引中，可能仅作为状态标记 / 答案分支 / 未被任何 mod 真实定义）`;
  const baseReason = matches
    ? wantSeen
      ? `前置事件已满足：已触发事件 ${target}`
      : `前置事件已满足：未触发事件 ${target}`
    : wantSeen
      ? `前置事件未满足：需要先触发事件 ${target}`
      : `前置事件不满足：事件 ${target} 已经触发`;

  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: `${baseReason}${indexNote}`,
  };
}

function evaluateWeather(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, "天气条件缺少目标天气。");
  }

  if (!state.weather) {
    return unknownEvaluation(condition, "运行时未提供天气信息。");
  }

  const current = normalizeNameKey(state.weather);
  const target = normalizeNameKey(condition.target);
  const isMatch = current === target || (target === "rainy" && current === "rain");
  const wantMatch = condition.type === "weather";
  const matches = wantMatch ? isMatch : !isMatch;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `天气满足：当前为${formatWeatherZh(state.weather)}`
      : `天气不满足：需要${wantMatch ? formatWeatherZh(condition.target) : `非${formatWeatherZh(condition.target)}`}，当前为${formatWeatherZh(state.weather)}`,
  };
}

function evaluateTime(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  if (!isTimeRange(condition.value)) {
    return unknownEvaluation(condition, "时间范围格式无效。");
  }

  if (typeof state.time !== "number") {
    return unknownEvaluation(condition, "运行时未提供当前时间。");
  }

  const { start, end } = condition.value;
  const inside = state.time >= start && state.time <= end;
  const matches = condition.negated ? !inside : inside;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `时间满足：当前时间为 ${formatTimeRangeZh(state.time, state.time).slice(0, 5)}`
      : `时间不满足：需要在 ${formatTimeRangeZh(start, end)}，当前为 ${formatTimeRangeZh(state.time, state.time).slice(0, 5)}`,
  };
}

function evaluateSeasonDay(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  const payload = condition.value as { season?: string; day?: number } | undefined;
  if (!payload?.season || typeof payload.day !== "number") {
    return unknownEvaluation(condition, "SEASON_DAY 参数无效。");
  }

  if (!state.season || typeof state.day !== "number") {
    return unknownEvaluation(condition, "运行时未提供季节或日期。");
  }

  const seasonMatch = normalizeNameKey(state.season) === normalizeNameKey(payload.season);
  const dayMatch = state.day === payload.day;
  const inside = seasonMatch && dayMatch;
  const matches = condition.negated ? !inside : inside;

  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `日期满足：当前为${formatSeasonZh(state.season)}第 ${state.day} 天`
      : `日期不满足：需要${condition.negated ? "不是" : ""}${formatSeasonZh(payload.season)}第 ${payload.day} 天，当前为${formatSeasonZh(state.season)}第 ${state.day} 天`,
    expectedZh: `${formatSeasonZh(payload.season)}第 ${payload.day} 天`,
    actualZh: `${formatSeasonZh(state.season)}第 ${state.day} 天`,
  };
}

function evaluateRelationshipStates(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  const allowed = Array.isArray(condition.value) ? condition.value.map(String) : [];
  if (!condition.target || allowed.length === 0) {
    return unknownEvaluation(condition, "关系条件参数不足。");
  }

  if (!hasAnyRelationshipRuntimeData(state)) {
    return unknownEvaluation(condition, "运行时未提供关系状态。");
  }

  const actualStates = relationshipStateValuesForNpc(state, condition.target);
  const matchAny = allowed.some((expected) =>
    actualStates.some((actual) => normalizeNameKey(actual) === normalizeNameKey(expected)),
  );
  const matches = condition.negated ? !matchAny : matchAny;
  const npcLabel = npcZh(condition.target);
  const allowedLabel = allowed.map(relationshipStateZh).join(" 或 ");

  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `${npcLabel}关系满足 ${allowedLabel}`
      : `${npcLabel}当前关系为 ${actualStates.length > 0 ? actualStates.map(relationshipStateZh).join("、") : "无"}，要求 ${allowedLabel}`,
    expectedZh: allowedLabel,
    actualZh: actualStates.map(relationshipStateZh).join("、"),
  };
}

function evaluateSeason(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  const seasons = Array.isArray(condition.value) ? condition.value : [];
  if (seasons.length === 0) {
    return unknownEvaluation(condition, "季节条件缺少参数。");
  }

  if (!state.season) {
    return unknownEvaluation(condition, "运行时未提供季节信息。");
  }

  const current = normalizeNameKey(state.season);
  const matchAny = seasons.some((season) => normalizeNameKey(season) === current);
  const matches = condition.negated ? !matchAny : matchAny;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `季节满足：当前为${formatSeasonZh(state.season)}`
      : `季节不满足：需要${condition.negated ? "不是" : ""}${seasons.map((season) => formatSeasonZh(season)).join(" / ")}，当前为${formatSeasonZh(state.season)}`,
    expectedZh: seasons.map((season) => formatSeasonZh(season)).join(" / "),
    actualZh: formatSeasonZh(state.season),
  };
}

function evaluateYear(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  const threshold = asNumber(condition.value);
  if (threshold === null) {
    return unknownEvaluation(condition, "年份条件无效。");
  }

  if (typeof state.year !== "number") {
    return unknownEvaluation(condition, "运行时未提供年份信息。");
  }

  const passed = state.year >= threshold;
  const matches = condition.negated ? !passed : passed;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `年份满足：当前为第 ${state.year} 年`
      : `年份不满足：需要第 ${threshold} 年或之后，当前为第 ${state.year} 年`,
  };
}

function evaluateSpouse(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, "婚姻条件缺少角色名。");
  }

  const relationships = getRelationshipNames(state);
  if (
    state.marriedTo === undefined &&
    state.spouse === undefined &&
    state.spouses === undefined &&
    state.engagedTo === undefined &&
    state.roommate === undefined
  ) {
    return unknownEvaluation(condition, "运行时未提供婚姻状态。");
  }

  const matched = hasRelationshipWith(state, condition.target);
  const wantMatch = condition.type === "spouse";
  const matches = wantMatch ? matched : !matched;
  const currentLabel =
    relationships.length > 0
      ? relationships.map((name) => npcZh(name)).join("、")
      : "未婚 / 未订婚 / 无室友";

  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? wantMatch
        ? `婚姻满足：玩家已和${npcZh(condition.target)}结婚或订婚`
        : `婚姻满足：玩家没有和${npcZh(condition.target)}结婚或订婚`
      : wantMatch
        ? `婚姻不满足：需要和${npcZh(condition.target)}结婚或订婚`
        : `婚姻不满足：玩家当前和${npcZh(condition.target)}处于结婚、订婚或室友关系`,
    expectedZh: wantMatch
      ? `和${npcZh(condition.target)}结婚或订婚`
      : `没有和${npcZh(condition.target)}结婚或订婚`,
    actualZh: currentLabel,
  };
}

function evaluateMail(
  condition: ParsedCondition,
  state: CurrentGameState,
  mailLabel: string,
): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, `${mailLabel}条件缺少标记名。`);
  }

  if (!state.mailFlags) {
    return unknownEvaluation(condition, "运行时未提供邮件标记。");
  }

  const has = state.mailFlags.includes(condition.target);
  const wantHas = !condition.negated;
  const matches = wantHas ? has : !has;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `${mailLabel}满足：${condition.target}`
      : `${mailLabel}不满足：需要${wantHas ? "拥有" : "未拥有"} ${condition.target}`,
  };
}

function evaluateActiveDialogue(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  if (!condition.target) {
    return unknownEvaluation(condition, "活跃对话主题条件缺少主题名。");
  }

  if (!state.conversationTopics) {
    return unknownEvaluation(condition, "运行时未提供活跃对话主题。");
  }

  const active = state.conversationTopics.includes(condition.target);
  const wantActive = condition.type === "activeDialogueEvent";
  const matches = wantActive ? active : !active;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `对话主题满足：${condition.target}`
      : `对话主题不满足：需要${wantActive ? "存在" : "不存在"}主题 ${condition.target}`,
  };
}

function evaluateDayOfMonth(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  const values = Array.isArray(condition.value) ? condition.value : [];
  if (values.length === 0) {
    return unknownEvaluation(condition, "日期条件缺少参数。");
  }

  if (typeof state.day !== "number") {
    return unknownEvaluation(condition, "运行时未提供日期。");
  }

  const matchAny = values.some((value) => Number.parseInt(value, 10) === state.day);
  const matches = condition.negated ? !matchAny : matchAny;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? `日期满足：今天是 ${state.day} 日`
      : `日期不满足：需要 ${values.join(" / ")} 日，当前是 ${state.day} 日`,
  };
}

function evaluateFestivalDay(condition: ParsedCondition, state: CurrentGameState): AtomEvaluation {
  {
    const wantFestival = condition.type === "festivalDay";
    if (typeof state.isFestivalDay !== "boolean") {
      if (!wantFestival) {
        return {
          outcome: "satisfied",
          reasonZh: "节日条件满足：当前未检测到节日。",
        };
      }

      return unknownEvaluation(condition, "运行时未提供节日状态。");
    }

    const matches = wantFestival ? state.isFestivalDay : !state.isFestivalDay;
    return {
      outcome: matches ? "satisfied" : "unsatisfied",
      reasonZh: matches
        ? wantFestival
          ? "节日条件满足：今天是节日。"
          : "节日条件满足：今天不是节日。"
        : wantFestival
          ? "节日条件不满足：需要今天是节日。"
          : "节日条件不满足：需要今天不是节日。",
    };
  }
  if (typeof state.isFestivalDay !== "boolean") {
    return unknownEvaluation(condition, "杩愯鏃舵湭鎻愪緵鑺傛棩鐘舵€併€?");
  }

  const wantFestival = condition.type === "festivalDay";
  const matches = wantFestival ? state.isFestivalDay : !state.isFestivalDay;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? wantFestival
        ? "鑺傛棩鏉′欢婊¤冻锛氫粖澶╂槸鑺傛棩"
        : "鑺傛棩鏉′欢婊¤冻锛氫粖澶╀笉鏄妭鏃?"
      : wantFestival
        ? "鑺傛棩鏉′欢涓嶆弧瓒筹細闇€瑕佷粖澶╂槸鑺傛棩"
        : "鑺傛棩鏉′欢涓嶆弧瓒筹細闇€瑕佷粖澶╀笉鏄妭鏃?",
  };
}

function evaluateInUpgradedHouse(
  condition: ParsedCondition,
  state: CurrentGameState,
): AtomEvaluation {
  if (typeof state.inUpgradedHouse !== "boolean") {
    return unknownEvaluation(condition, "当前暂无法判断：是否在升级后的农舍或小屋中。");
  }

  const matches = condition.negated ? !state.inUpgradedHouse : state.inUpgradedHouse;
  return {
    outcome: matches ? "satisfied" : "unsatisfied",
    reasonZh: matches
      ? condition.negated
        ? "房屋条件满足：当前不在升级后的农舍或小屋中"
        : "房屋条件满足：当前在升级后的农舍或小屋中"
      : condition.negated
        ? "房屋条件不满足：需要当前不在升级后的农舍或小屋中"
        : "房屋条件不满足：需要当前在升级后的农舍或小屋中",
  };
}

function parsePatchWhenContainsClause(key?: string): { npc?: string; values: string[] } | null {
  if (!key?.startsWith("Relationship:")) {
    return null;
  }

  const [, remainder = ""] = key.split(":", 2);
  const [npcPart, modifierPart = ""] = remainder.split("|", 2);
  const [operator, rawValues = ""] = modifierPart.split("=", 2);
  if (operator.trim() !== "contains") {
    return null;
  }

  const values = rawValues
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item.length > 0);

  return {
    npc: npcPart.trim() || undefined,
    values,
  };
}

function relationshipContainsDescriptionZh(npc: string, values: string[], expected: boolean): string {
  const npcLabel = npcZh(npc);
  const stateLabel = values.map(relationshipStateZh).join("、");
  return expected
    ? `需要和${npcLabel}处于${stateLabel}状态`
    : `不能和${npcLabel}处于${stateLabel}状态`;
}

function createRelationshipContainsPatchItem(
  patch: NonNullable<StoryNodeEvaluation["patchWhenConditions"]>[number],
  state: CurrentGameState,
): DiagnosticItem | null {
  const contains = parsePatchWhenContainsClause(patch.key);
  if (!contains?.npc || contains.values.length === 0) {
    return null;
  }

  if (!hasAnyRelationshipRuntimeData(state)) {
    return null;
  }

  const expected = parseBooleanLike(patch.value ?? patch.rawValue);
  const actualStates = relationshipStateValuesForNpc(state, contains.npc);
  const hasExpectedState = contains.values.some((value) =>
    actualStates.some((actual) => normalizeNameKey(actual) === normalizeNameKey(value)),
  );
  const passed = expected ? hasExpectedState : !hasExpectedState;
  const descriptionZh = relationshipContainsDescriptionZh(contains.npc, contains.values, expected);

  return {
    conditionRaw: `${patch.key ?? "When"}: ${patch.value ?? patch.rawValue ?? ""}`,
    status: passed ? "satisfied" : "unsatisfied",
    type: "unknown",
    negated: !expected,
    descriptionZh,
    reasonZh: passed ? descriptionZh : `${descriptionZh}，当前不满足。`,
    reasonRaw: patch.reason,
    expectedZh: contains.values.map(relationshipStateZh).join("、"),
    actualZh: actualStates.length > 0 ? actualStates.map(relationshipStateZh).join("、") : "无",
  };
}

function createCampoutDaysItem(raw: string, state: CurrentGameState): DiagnosticItem | null {
  const tokenMatch = raw.trim().match(/^\{\{([A-Za-z0-9_]+)\}\}$/);
  if (!tokenMatch) {
    return null;
  }

  const tokenName = tokenMatch[1];
  const knownSummaries: Record<string, string> = {
    FrogDays: "青蛙约会可用日期：由该 Mod 的 DynamicTokens 自动展开",
    MineDays: "矿井约会可用日期：由该 Mod 的 DynamicTokens 自动展开",
    OverlookDays: "眺望约会可用日期：由该 Mod 的 DynamicTokens 自动展开",
    PoolDays: "泳池约会可用日期：由该 Mod 的 DynamicTokens 自动展开",
  };

  if (tokenName !== "CampoutDays") {
    const descriptionZh = knownSummaries[tokenName] ?? "该 Mod 自定义条件，详情见 Debug";
    return {
      conditionRaw: raw,
      status: "satisfied",
      type: "unknown",
      negated: false,
      descriptionZh,
      reasonZh: descriptionZh,
      reasonRaw: `DynamicToken ${raw} is expanded by the backend token registry when available.`,
    };
  }

  {
    const descriptionZh = "露营约会日期：春季 12/19/20 或秋季 13/14/18";
    const season = state.season?.trim().toLowerCase();
    const day = typeof state.day === "number" ? state.day : null;
    const isCampoutDate =
      (season === "spring" && day !== null && [12, 19, 20].includes(day)) ||
      (season === "fall" && day !== null && [13, 14, 18].includes(day));
    const isKnownDate = !!season && day !== null;
    const status: DiagnosticItem["status"] = !isKnownDate || isCampoutDate ? "satisfied" : "unsatisfied";

    return {
      conditionRaw: raw,
      status,
      type: "unknown",
      negated: false,
      descriptionZh,
      reasonZh:
        status === "satisfied"
          ? isKnownDate
            ? `露营约会日期满足：当前是${formatSeasonZh(season)} ${day} 日。`
            : `${descriptionZh}。当前运行时缺少季节或日期，按已展开的候选日期显示。`
          : `露营约会日期不满足：当前是${formatSeasonZh(season)} ${day} 日。`,
      reasonRaw: "DynamicToken {{CampoutDays}} expanded from content.json DynamicTokens.",
      expectedZh: "春季 12/19/20 或秋季 13/14/18",
      actualZh: isKnownDate ? `${formatSeasonZh(season)} ${day} 日` : "缺少当前季节或日期",
    };
  }

  if (raw.trim() !== "{{CampoutDays}}") {
    return null;
  }

  const descriptionZh = "露营约会日期：春季 12/19/20 或秋季 13/14/18";
  const season = state.season?.trim().toLowerCase();
  const day = typeof state.day === "number" ? state.day : null;
  const isCampoutDate =
    (season === "spring" && day !== null && [12, 19, 20].includes(day)) ||
    (season === "fall" && day !== null && [13, 14, 18].includes(day));
  const isKnownDate = !!season && day !== null;
  const status: DiagnosticItem["status"] = !isKnownDate || isCampoutDate ? "satisfied" : "unsatisfied";

  return {
    conditionRaw: raw,
    status,
    type: "unknown",
    negated: false,
    descriptionZh,
    reasonZh:
      status === "satisfied"
        ? isKnownDate
          ? `露营约会日期满足：当前是${formatSeasonZh(season)} ${day} 日。`
          : `${descriptionZh}。当前运行时缺少季节或日期，按已展开的候选日期显示。`
        : `露营约会日期不满足：当前是${formatSeasonZh(season)} ${day} 日。`,
    reasonRaw: "DynamicToken {{CampoutDays}} expanded from content.json DynamicTokens.",
    expectedZh: "春季 12/19/20 或秋季 13/14/18",
    actualZh: isKnownDate ? `${formatSeasonZh(season)} ${day} 日` : "缺少当前季节或日期",
  };
}

function formatRelationshipContainsDescriptionZh(
  key?: string,
  value?: string,
): string | null {
  const contains = parsePatchWhenContainsClause(key);
  if (!contains?.npc || contains.values.length === 0) {
    return null;
  }

  return relationshipContainsDescriptionZh(contains.npc, contains.values, parseBooleanLike(value));
}

function parseYearsMarriedQuery(value?: string): { operator: string; threshold: string } | null {
  const match = (value ?? "").match(/YearsMarried\}\}['"]?\s*(>=|<=|=|==|>|<)\s*(\d+)/i);
  if (!match) {
    return null;
  }

  return {
    operator: match[1],
    threshold: match[2],
  };
}

function formatYearsMarriedExpectationZh(operator: string, threshold: string): string {
  switch (operator) {
    case ">=":
      return `结婚年数至少 ${threshold} 年`;
    case ">":
      return `结婚年数超过 ${threshold} 年`;
    case "<=":
      return `结婚年数最多 ${threshold} 年`;
    case "<":
      return `结婚年数少于 ${threshold} 年`;
    case "=":
    case "==":
      return `结婚年数为 ${threshold} 年`;
    default:
      return `结婚年数满足 ${operator} ${threshold}`;
  }
}

function parseYearsMarriedActualFromReason(reason?: string): string | null {
  return reason?.match(/value is (\d+)/i)?.[1] ?? null;
}

function formatPatchWhenDescriptionZh(key?: string, value?: string): string {
  if (!key) {
    return "CP When 条件";
  }

  if (key === "Query") {
    const yearsMarried = parseYearsMarriedQuery(value);
    if (yearsMarried) {
      return formatYearsMarriedExpectationZh(yearsMarried.operator, yearsMarried.threshold);
    }
  }

  if (key.startsWith("Hearts:")) {
    const npc = key.slice("Hearts:".length).split("|")[0]?.trim();
    return npc ? `CP 好感度条件：${npcZh(npc)} = ${value ?? "?"} 心` : `CP 好感度条件：${key}`;
  }

  if (key.startsWith("Relationship:")) {
    const containsDescription = formatRelationshipContainsDescriptionZh(key, value);
    if (containsDescription) {
      return containsDescription;
    }

    const npc = key.slice("Relationship:".length).split("|")[0]?.trim();
    return npc ? `CP 关系条件：${npcZh(npc)}` : `CP 关系条件：${key}`;
  }

  if (key.startsWith("HasMod")) {
    return "CP 模组安装条件";
  }

  if (key === "DayEvent" || key.startsWith("DayEvent ")) {
    return value ? `节日/特殊日前置条件：需要 ${value}` : "节日/特殊日前置条件";
  }

  if (key === "FarmerCheater") {
    return "CP 多伴侣兼容条件";
  }

  return `CP When 条件：${key}`;
}

function formatKnownPatchWhenReasonZh(patch: NonNullable<StoryNodeEvaluation["patchWhenConditions"]>[number]): string {
  const key = patch.key ?? "When";
  const value = patch.value ?? patch.rawValue ?? "";

  if (key === "Query") {
    const yearsMarried = parseYearsMarriedQuery(value);
    if (yearsMarried) {
      const expectation = formatYearsMarriedExpectationZh(yearsMarried.operator, yearsMarried.threshold);
      const actual = parseYearsMarriedActualFromReason(patch.reason);
      const actualText = actual ? `当前为 ${actual} 年` : "当前年数未知";
      return patch.passed === false
        ? `${expectation}，${actualText}，条件不满足`
        : `${expectation}，${actualText}，条件已满足`;
    }
  }

  if (key.startsWith("Hearts:")) {
    const npc = key.slice("Hearts:".length).split("|")[0]?.trim();
    const npcLabel = npc ? npcZh(npc) : key;
    return patch.passed === false
      ? `好感度不满足：需要 ${npcLabel} 为 ${value} 心`
      : `好感度已满足：${npcLabel} 当前符合 ${value} 心条件`;
  }

  if (key.startsWith("Relationship:")) {
    const containsDescription = formatRelationshipContainsDescriptionZh(key, value);
    if (containsDescription) {
      return patch.passed === false
        ? `${containsDescription}，当前状态不符合`
        : containsDescription;
    }

    const npc = key.slice("Relationship:".length).split("|")[0]?.trim();
    const npcLabel = npc ? npcZh(npc) : key;
    return patch.passed === false
      ? `关系不满足：${npcLabel} 的关系状态不符合要求`
      : `关系已满足：${npcLabel} 的关系状态符合要求`;
  }

  if (key.startsWith("HasMod")) {
    return patch.passed === false
      ? "模组条件不满足：当前已安装模组不符合该剧情要求"
      : "模组条件已满足：当前已安装模组符合该剧情要求";
  }

  if (key === "DayEvent" || key.startsWith("DayEvent ")) {
    if (patch.reasonZh) {
      return patch.reasonZh;
    }

    return patch.passed === false
      ? `节日/特殊日条件不满足：需要 ${value}`
      : `节日/特殊日条件已满足：${value}`;
  }

  if (key === "FarmerCheater") {
    return patch.passed === false
      ? "多伴侣兼容条件不满足：当前玩家状态不符合该剧情要求"
      : "多伴侣兼容条件已满足";
  }

  return patch.passed === false
    ? `CP When 条件不满足：${key}`
    : `CP When 条件已满足：${key}`;
}

function createItem(condition: ParsedCondition, evaluation: AtomEvaluation): DiagnosticItem {
  return {
    conditionRaw: condition.raw,
    status: evaluation.outcome,
    type: condition.type,
    negated: condition.negated,
    descriptionZh: formatConditionZh(condition),
    descriptionRaw: condition.descriptionRaw,
    reasonZh: evaluation.reasonZh,
    reasonRaw: evaluation.reasonRaw,
    expectedZh: evaluation.expectedZh,
    actualZh: evaluation.actualZh,
  };
}

function createResolvedMinFriendshipItem(
  condition: ParsedCondition,
  node: StoryNodeEvaluation,
): DiagnosticItem | null {
  if (condition.type !== "friendship" || typeof condition.value !== "string" || !/MinFriendship/i.test(condition.value)) {
    return null;
  }

  const atom = node.conditionResult?.atomResults?.find((entry) =>
    entry.atomType === "Friendship"
    && typeof entry.raw === "string"
    && /\s\d+\s*$/.test(entry.raw)
    && entry.passed !== null
    && entry.passed !== undefined,
  );
  if (!atom?.raw) {
    return null;
  }

  const parsed = atom.raw.match(/(?:Friendship|f)\s+(.+?)\s+(\d+)\s*$/i);
  const target = parsed?.[1]?.trim() || condition.target || "目标角色";
  const threshold = parsed?.[2]?.trim() || "动态阈值";
  const actual = atom.reason?.match(/has\s+(\d+)/i)?.[1];
  const status: EvalOutcome = atom.passed ? "satisfied" : "unsatisfied";

  return {
    conditionRaw: condition.raw,
    status,
    type: "friendship",
    negated: condition.negated,
    descriptionZh: `${npcZh(target)}好感度至少 ${threshold}`,
    reasonZh: atom.passed
      ? `好感度满足：${npcZh(target)}当前好感度已达到 ${actual ?? threshold}`
      : `好感度不满足：${npcZh(target)}当前好感度为 ${actual ?? "未知"}，需要至少 ${threshold}`,
    reasonRaw: atom.reason,
    expectedZh: `${npcZh(target)}好感度至少 ${threshold}`,
    actualZh: actual ? `${npcZh(target)}好感度 ${actual}` : undefined,
  };
}

function evaluateLocation(node: StoryNodeEvaluation, state: CurrentGameState): DiagnosticItem | null {
  const required = node.location?.trim();
  if (!required) {
    return null;
  }

  if (!state.location) {
    return {
      conditionRaw: `location ${required}`,
      status: "unknown",
      type: "unknown",
      negated: false,
      descriptionZh: `触发地点：${formatLocationZh(required, node.sourceModId)}`,
      reasonZh: `当前暂无法判断地点：需要在「${formatLocationZh(required, node.sourceModId)}」`,
    };
  }

  const matches = normalizeNameKey(state.location) === normalizeNameKey(required);
  const requiredZh = formatLocationZh(required, node.sourceModId);
  const actualZh = formatLocationZh(state.location, node.sourceModId);
  return {
    conditionRaw: `location ${required}`,
    status: matches ? "satisfied" : "unsatisfied",
    type: "unknown",
    negated: false,
    descriptionZh: `触发地点：${requiredZh}（raw: ${required}）`,
    reasonZh: matches
      ? `地点满足：当前在「${requiredZh}」（raw: ${state.location}）`
      : `地点不满足：需要在「${requiredZh}」（raw: ${required}），当前在「${actualZh}」（raw: ${state.location}）`,
    expectedZh: `${requiredZh}（${required}）`,
    actualZh: `${actualZh}（${state.location}）`,
  };
}

export function formatDiagnosticZh(item: DiagnosticItem): string {
  return item.reasonZh;
}

export function diagnoseEventTrigger(
  node: StoryNodeEvaluation,
  state: CurrentGameState,
  options?: DiagnoseOptions,
): DiagnosisResult {
  const dynamicTokenItems: DiagnosticItem[] = [];
  const rawPreconditions = (node.rawPreconditions ?? []).filter((raw) => {
    const dynamicTokenItem = createCampoutDaysItem(raw, state);
    if (dynamicTokenItem) {
      dynamicTokenItems.push(dynamicTokenItem);
      return false;
    }

    return true;
  });
  const conditions = parseConditions(rawPreconditions);

  const satisfied: DiagnosticItem[] = [];
  const unsatisfied: DiagnosticItem[] = [];
  const unknown: DiagnosticItem[] = [];

  const locationDiag = evaluateLocation(node, state);
  if (locationDiag) {
    if (locationDiag.status === "satisfied") {
      satisfied.push(locationDiag);
    } else if (locationDiag.status === "unsatisfied") {
      unsatisfied.push(locationDiag);
    } else {
      unknown.push(locationDiag);
    }
  }

  for (const item of dynamicTokenItems) {
    if (item.status === "satisfied") {
      satisfied.push(item);
    } else if (item.status === "unsatisfied") {
      unsatisfied.push(item);
    } else {
      unknown.push(item);
    }
  }

  for (const condition of conditions) {
    const resolvedMinFriendshipItem = createResolvedMinFriendshipItem(condition, node);
    if (resolvedMinFriendshipItem) {
      if (resolvedMinFriendshipItem.status === "satisfied") {
        satisfied.push(resolvedMinFriendshipItem);
      } else {
        unsatisfied.push(resolvedMinFriendshipItem);
      }

      continue;
    }

    const evaluation = evaluateAtom(condition, state, options);
    const item = createItem(condition, evaluation);

    if (evaluation.outcome === "satisfied") {
      satisfied.push(item);
    } else if (evaluation.outcome === "unsatisfied") {
      unsatisfied.push(item);
    } else {
      unknown.push(item);
    }
  }

  for (const patch of node.patchWhenConditions ?? []) {
    if (patch.isKnown) {
      const relationshipItem = createRelationshipContainsPatchItem(patch, state);
      if (relationshipItem) {
        if (relationshipItem.status === "unsatisfied") {
          unsatisfied.push(relationshipItem);
        } else {
          satisfied.push(relationshipItem);
        }

        continue;
      }

      const knownItem: DiagnosticItem = {
        conditionRaw: `${patch.key ?? "When"}: ${patch.value ?? patch.rawValue ?? ""}`,
        status: patch.passed === false ? "unsatisfied" : "satisfied",
        type: "unknown",
        negated: false,
        descriptionZh: formatPatchWhenDescriptionZh(patch.key, patch.value),
        reasonZh: formatKnownPatchWhenReasonZh(patch),
        reasonRaw: patch.reason,
        actualZh: patch.value ?? patch.rawValue ?? "",
      };

      if (patch.passed === false) {
        unsatisfied.push(knownItem);
      } else {
        satisfied.push(knownItem);
      }

      continue;
    }

    const relationshipItem = createRelationshipContainsPatchItem(patch, state);
    if (relationshipItem) {
      if (relationshipItem.status === "unsatisfied") {
        unsatisfied.push(relationshipItem);
      } else {
        satisfied.push(relationshipItem);
      }

      continue;
    }

    const patchRaw = `${patch.key ?? "When"}: ${patch.value ?? patch.rawValue ?? ""}`;
    if (patch.unknownKind === "runtimeMissing") {
      const runtimeMissingPrefix = "\u65e0\u6cd5\u5224\u65ad\uff1a";
      const runtimeMissingFallback = "\u8fd0\u884c\u65f6\u72b6\u6001\u7f3a\u5931";
      const noRawValue = "\u65e0\u539f\u59cb\u503c";
      const reasonZh = patch.reasonZh?.startsWith(runtimeMissingPrefix)
        ? patch.reasonZh
        : `${runtimeMissingPrefix}${patch.reasonZh ?? patch.reason ?? runtimeMissingFallback}`;
      unknown.push({
        conditionRaw: patchRaw,
        status: "unknown",
        type: "unknown",
        negated: false,
        descriptionZh: formatPatchWhenDescriptionZh(patch.key, patch.value),
        reasonZh,
        reasonRaw: patch.reason,
        actualZh: patch.value ?? patch.rawValue ?? noRawValue,
      });
      continue;
    }


    if (patch.unknownKind === "complexQueryUnsupported") {
      unknown.push({
        conditionRaw: patchRaw,
        status: "unknown",
        type: "unknown",
        negated: false,
        descriptionZh: "随机/概率 CP Query",
        reasonZh: patch.reasonZh ?? "随机/概率条件暂不展开。",
        reasonRaw: patch.reason,
        actualZh: patch.value ?? patch.rawValue ?? "无原始值",
      });
      continue;
    }

    if (
      patch.unknownKind === "externalTokenMissing"
      || patch.unknownKind === "randomTokenUnsupported"
    ) {
      unknown.push({
        conditionRaw: patchRaw,
        status: "unknown",
        type: "unknown",
        negated: false,
        descriptionZh: formatPatchWhenDescriptionZh(patch.key, patch.value),
        reasonZh: patch.reasonZh ?? patch.reason ?? "外部或随机条件暂无法评估。",
        reasonRaw: patch.reason,
        actualZh: patch.value ?? patch.rawValue ?? "无原始值",
      });
      continue;
    }

    unknown.push({
      conditionRaw: patchRaw,
      status: "unknown",
      type: "unknown",
      negated: false,
      descriptionZh: `CP When 条件：${patch.key ?? "未知键"}`,
      reasonZh: `未解析条件：${patch.key ?? "When"}`,
      reasonRaw: patch.reason,
      actualZh: patch.value ?? patch.rawValue ?? "无原始值",
    });
  }

  return {
    canTrigger: unsatisfied.length === 0 && unknown.length === 0,
    satisfied,
    unsatisfied,
    unknown,
  };
}

export function buildGameStateFromRuntime(runtime?: RuntimeGameState | null): CurrentGameState {
  if (!runtime) {
    return {};
  }

  const state: CurrentGameState = {
    year: runtime.year,
    season: runtime.season,
    day: runtime.dayOfMonth,
    dayOfWeek: runtime.dayOfWeek,
    time: runtime.time,
    location: runtime.currentLocation,
    weather: runtime.weather,
    isFestivalDay: runtime.isFestivalDay,
    installedModIds: runtime.installedModIds ?? undefined,
    friendship: runtime.friendshipPoints ?? {},
    seenEvents: runtime.seenEvents ?? [],
    mailFlags: runtime.mail ?? [],
    conversationTopics: runtime.dialogueAnswers ?? [],
    visibleNpcNamesHere: runtime.visibleNpcNamesHere ?? undefined,
    inUpgradedHouse: runtime.inUpgradedHouse,
    spouseBedKnown: runtime.spouseBedKnown,
    hasSpouseBed: runtime.hasSpouseBed,
  };

  // Only populate relationship fields when the runtime actually supplied them.
  // Synthesizing null/[] would make every spouse/dating check resolve to
  // "unsatisfied" instead of "unknown" — a regression we explicitly avoid.
  if (runtime.datingNpcNames !== undefined) {
    state.dating = runtime.datingNpcNames;
  }

  if (runtime.spouseName !== undefined && runtime.spouseName !== null) {
    state.marriedTo = runtime.spouseName;
    state.spouse = runtime.spouseName;
    state.spouses = runtime.spouses ?? [runtime.spouseName];
  } else if (runtime.marriedTo !== undefined) {
    state.marriedTo = runtime.marriedTo;
  } else if (runtime.spouse !== undefined) {
    state.spouse = runtime.spouse;
  } else if (runtime.spouses !== undefined) {
    state.spouses = runtime.spouses ?? undefined;
  }

  if (runtime.engagedTo !== undefined) {
    state.engagedTo = runtime.engagedTo;
  }

  if (runtime.roommate !== undefined) {
    state.roommate = runtime.roommate;
  }

  return state;
}
