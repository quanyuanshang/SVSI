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
  friendshipPoints: Record<string, number>;
  seenEvents: string[];
  mail: string[];
  dialogueAnswers: string[];
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
}
