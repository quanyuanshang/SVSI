export type StoryNodeStatus =
  | "Triggered"
  | "Current"
  | "AvailableLater"
  | "Locked"
  | "Unknown";

export interface RuntimeGameState {
  year: number;
  season: string;
  dayOfMonth: number;
  dayOfWeek: string;
  time: number;
  weather: string;
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
  seenEvents: string[];
  mail: string[];
  dialogueAnswers: string[];
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
}

export interface PatchWhenCondition {
  key?: string;
  value?: string;
  rawValue?: string;
  isKnown?: boolean;
  passed?: boolean | null;
  isProgressionSensitive?: boolean;
  reason?: string;
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

export interface StoryStateEvaluationReport {
  generatedAtUtc?: string;
  runtimeState?: RuntimeGameState;
  translationCatalog?: TranslationCatalog;
  totalNodeCount?: number;
  statusCounts?: Partial<Record<StoryNodeStatus, number>>;
  nodes?: StoryNodeEvaluation[];
}

export interface StoryFilterState {
  selectedStatuses: Set<StoryNodeStatus>;
  selectedModNames: Set<string>;
  selectedLocations: Set<string>;
  selectedNpcNames: Set<string>;
  hideTriggered: boolean;
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
