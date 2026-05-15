export type StoryNodeStatus =
  | "Triggered"
  | "Current"
  | "AvailableLater"
  | "Locked"
  | "Unknown"
  | "NonTriggerable"
  | "BranchTarget"
  | "SpecialEvent";

export type StoryNodeEventKind =
  | "RegularLocationEvent"
  | "BranchTarget"
  | "SpecialGameEvent"
  | "DialogueOnly"
  | "InvalidOrUnsupported";

export interface RuntimeGameState {
  year: number;
  season: string;
  dayOfMonth: number;
  dayOfWeek: string;
  time: number;
  weather: string;
  isFestivalDay?: boolean | null;
  currentLocation: string;
  playerName: string;
  installedModIds?: string[];
  friendshipPoints: Record<string, number>;
  spouseName?: string | null;
  spouse?: string | string[] | null;
  marriedTo?: string | string[] | null;
  spouses?: string[] | null;
  engagedTo?: string | string[] | null;
  roommate?: string | string[] | null;
  datingNpcNames?: string[];
  visibleNpcNamesHere?: string[];
  inUpgradedHouse?: boolean | null;
  spouseBedKnown?: boolean;
  hasSpouseBed?: boolean | null;
  farmhouseUpgradeKnown?: boolean;
  farmhouseUpgradeLevel?: number | null;
  seenEvents: string[];
  mail: string[];
  dialogueAnswers: string[];
  activeDialogueEventsKnown?: boolean;
  activeDialogueEvents?: string[];
  dayEventsKnown?: boolean;
  dayEvents?: string[];
}

export interface TranslationEntry {
  category: string;
  raw: string;
  zh: string;
  source: string;
  sourceModId?: string;
  sourceModName?: string;
  sourcePath?: string;
}

export interface TranslationWarning {
  message: string;
  sourceModId?: string;
  sourceModName?: string;
  sourcePath?: string;
}

export interface TranslationCatalog {
  entries?: TranslationEntry[];
  warnings?: TranslationWarning[];
}

export interface EvidenceRef {
  kind?: string;
  sourcePath?: string;
  jsonPath?: string;
}

export interface RelatedDialogueRef {
  npcName?: string;
  dialogueKey?: string;
  responseId?: string;
  previewText?: string;
  sourceModId?: string;
}

export interface RelatedEventChoiceRef {
  eventId?: string;
  assetTarget?: string;
  location?: string;
  npcName?: string;
  rawKey?: string;
  responseId?: string;
  previewText?: string;
  sourceModId?: string;
  sourceModName?: string;
}

export interface ConditionAtomResult {
  raw?: string;
  atomType?: string;
  passed?: boolean | null;
  isContextSensitive?: boolean;
  isProgressionSensitive?: boolean;
  reason?: string;
  unknownKind?: string;
  reasonZh?: string;
}

export interface PatchWhenCondition {
  key?: string;
  value?: string;
  rawValue?: string;
  isKnown?: boolean;
  passed?: boolean | null;
  isContextSensitive?: boolean;
  isProgressionSensitive?: boolean;
  reason?: string;
  unknownKind?: string;
  reasonZh?: string;
  parsedType?: string;
}

export interface ConditionEvaluationResult {
  passed?: boolean | null;
  hasUnknown?: boolean;
  reason?: string;
  atomResults?: ConditionAtomResult[];
}

export interface StoryNodeEvaluation {
  nodeId?: string;
  eventId?: string;
  eventKind?: StoryNodeEventKind;
  sourceModId?: string;
  sourceModName?: string;
  location?: string;
  rawKey?: string;
  rawPreconditions?: string[];
  unknownFragments?: string[];
  rawScriptPreview?: string;
  patchWhenConditions?: PatchWhenCondition[];
  status?: StoryNodeStatus;
  statusReason?: string;
  conditionResult?: ConditionEvaluationResult;
  evidenceRefs?: EvidenceRef[];
  relatedDialogueRefs?: RelatedDialogueRef[];
  relatedEventChoiceRefs?: RelatedEventChoiceRef[];
}

export interface UnknownConditionSummary {
  raw?: string;
  count?: number;
  sourceFiles?: string[];
  exampleEvents?: string[];
  suggestedParserType?: string;
}

export interface StoryStateEvaluationReport {
  generatedAtUtc?: string;
  runtimeState?: RuntimeGameState;
  translationCatalog?: TranslationCatalog;
  totalNodeCount?: number;
  statusCounts?: Partial<Record<StoryNodeStatus, number>>;
  unknownConditions?: UnknownConditionSummary[];
  nodes?: StoryNodeEvaluation[];
}

export interface StoryFilterState {
  selectedStatuses: Set<StoryNodeStatus>;
  selectedModNames: Set<string>;
  selectedLocations: Set<string>;
  selectedNpcNames: Set<string>;
  hideTriggered: boolean;
  hideNonTriggerable: boolean;
  searchText: string;
}

export interface StoryFilterOptions {
  statuses: StoryNodeStatus[];
  modNames: string[];
  locations: string[];
  npcNames: string[];
  // Maps any raw value (e.g. `FarmHouse`, `farmhouse`, `{{FarmHouse}}`) to the
  // set of raws that translate to the same zh label. Used by applyStoryFilters
  // to keep filtering on raw values per AGENTS.md while letting the UI present
  // a single de-dup'd entry per Chinese display name.
  locationEquivalents?: ReadonlyMap<string, ReadonlySet<string>>;
  npcEquivalents?: ReadonlyMap<string, ReadonlySet<string>>;
}
