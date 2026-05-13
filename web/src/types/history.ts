export interface GameDateSnapshot {
  year: number;
  season: string;
  dayOfMonth: number;
  time: number;
}

export interface SaveIdentity {
  farmerName?: string;
  farmName?: string;
  saveId?: string;
}

export interface ObservedEventHistoryEntry {
  eventId: string;
  nodeId?: string;
  sourceModId?: string;
  sourceModName?: string;
  observationSource?: string;
  firstSeenGameDate?: GameDateSnapshot;
  date?: GameDateSnapshot;
  location?: string;
  observedAtUtc?: string;
}

export interface EventHistoryReport {
  generatedAtUtc?: string;
  identity?: SaveIdentity;
  entries?: ObservedEventHistoryEntry[];
}
