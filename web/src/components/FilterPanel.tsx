import { CharacterPortrait } from "./CharacterPortrait";
import { StardewButton } from "./StardewButton";
import { StardewNineSlicePanel } from "./StardewNineSlicePanel";
import { formatStatusLabel } from "../lib/format";
import { formatNpcFilterLabel, translateLocation } from "../lib/translations";
import type {
  StoryFilterOptions,
  StoryFilterState,
  StoryNodeStatus,
} from "../types/story";

interface FilterPanelProps {
  statusCounts?: Partial<Record<StoryNodeStatus, number>>;
  totalNodeCount?: number;
  filters: StoryFilterState;
  availableOptions: StoryFilterOptions;
  onToggleStatus: (status: StoryNodeStatus) => void;
  onToggleModName: (modName: string) => void;
  onToggleLocation: (location: string) => void;
  onToggleNpcName: (npcName: string) => void;
  onHideTriggeredChange: (value: boolean) => void;
  onSearchTextChange: (value: string) => void;
  onClearFilters: () => void;
}

const STATUS_ORDER: StoryNodeStatus[] = [
  "Current",
  "AvailableLater",
  "Locked",
  "Unknown",
  "Triggered",
];

const FEATURED_CHARACTER_ORDER = [
  "Sebastian",
  "Sam",
  "Alex",
  "Wizard",
  "Harvey",
  "Lance",
  "Elliott",
  "Shane",
  "Victor",
  "Magnus",
];

export function FilterPanel({
  statusCounts,
  totalNodeCount,
  filters,
  availableOptions,
  onToggleStatus,
  onToggleModName,
  onToggleLocation,
  onToggleNpcName,
  onHideTriggeredChange,
  onSearchTextChange,
  onClearFilters,
}: FilterPanelProps) {
  const selectedNpcCount = filters.selectedNpcNames.size;
  const featuredNpcNames = getFeaturedNpcNames(availableOptions.npcNames);
  const remainingNpcNames = availableOptions.npcNames.filter(
    (npcName) => !featuredNpcNames.includes(npcName),
  );

  return (
    <StardewNineSlicePanel as="aside" className="panel filter-panel" variant="board">
      <div className="brand-card">
        <div className="brand-chicken" aria-hidden="true">SV</div>
        <div>
          <h1>Stardew</h1>
          <p>Story Inspector</p>
        </div>
      </div>

      <div className="summary-stat">
        <span className="summary-stat__label">事件总数</span>
        <strong>{totalNodeCount ?? 0}</strong>
      </div>

      <div className="filter-section">
        <label className="filter-label" htmlFor="story-search">
          搜索
        </label>
        <input
          className="filter-input"
          id="story-search"
          onChange={(event) => onSearchTextChange(event.target.value)}
          placeholder="搜索事件、Mod、角色、地点..."
          type="search"
          value={filters.searchText}
        />
      </div>

      <div className="filter-section">
        <div className="filter-label-row">
          <p className="filter-label">角色</p>
          <span>{selectedNpcCount ? `已选 ${selectedNpcCount}` : "角色图鉴入口"}</span>
        </div>
        <PortraitFilterGrid
          npcNames={featuredNpcNames}
          npcPortraitModIds={availableOptions.npcPortraitModIds}
          overflowNpcNames={remainingNpcNames}
          onToggleNpcName={onToggleNpcName}
          selectedNpcNames={filters.selectedNpcNames}
        />
      </div>

      <details className="filter-section" open>
        <summary className="filter-label">状态筛选</summary>
        <ul className="status-count-list">
          {STATUS_ORDER.map((status) => (
            <li className="status-count-row" key={status}>
              <label className="checkbox-row">
                <input
                  checked={filters.selectedStatuses.has(status)}
                  onChange={() => onToggleStatus(status)}
                  type="checkbox"
                />
                <span>{formatStatusLabel(status)}</span>
              </label>
              <strong>{statusCounts?.[status] ?? 0}</strong>
            </li>
          ))}
        </ul>
      </details>

      <details className="filter-section">
        <summary className="filter-label">Mod 筛选</summary>
        <FilterGroup
          options={availableOptions.modNames}
          selectedOptions={filters.selectedModNames}
          onToggleOption={onToggleModName}
        />
      </details>

      <details className="filter-section">
        <summary className="filter-label">地点</summary>
        <FilterGroup
          options={availableOptions.locations}
          selectedOptions={filters.selectedLocations}
          renderOption={(value) => translateLocation(value).zh}
          onToggleOption={onToggleLocation}
        />
      </details>

      <label className="checkbox-row filter-muted-row">
        <input
          checked={filters.hideTriggered}
          onChange={(event) => onHideTriggeredChange(event.target.checked)}
          type="checkbox"
        />
        <span>隐藏已触发事件</span>
      </label>

      <StardewButton className="clear-filter-button" onClick={onClearFilters} tone="quiet" type="button">
        清空筛选
      </StardewButton>
    </StardewNineSlicePanel>
  );
}

function getFeaturedNpcNames(npcNames: string[]): string[] {
  const selected: string[] = [];
  const byLower = new Map(npcNames.map((name) => [name.toLowerCase(), name]));

  for (const preferred of FEATURED_CHARACTER_ORDER) {
    const match = byLower.get(preferred.toLowerCase());
    if (match && !selected.includes(match)) {
      selected.push(match);
    }
  }

  for (const npcName of npcNames) {
    if (selected.length >= 10) {
      break;
    }

    if (!selected.includes(npcName)) {
      selected.push(npcName);
    }
  }

  return selected.slice(0, 10);
}

function PortraitFilterGrid({
  npcNames,
  selectedNpcNames,
  onToggleNpcName,
  overflowNpcNames = [],
  npcPortraitModIds,
}: {
  npcNames: string[];
  selectedNpcNames: ReadonlySet<string>;
  onToggleNpcName: (npcName: string) => void;
  overflowNpcNames?: string[];
  npcPortraitModIds?: ReadonlyMap<string, string>;
}) {
  return (
    <div className="portrait-filter-grid">
      {npcNames.map((npcName) => (
        <button
          className={`portrait-filter${selectedNpcNames.has(npcName) ? " portrait-filter--selected" : ""}`}
          key={npcName}
          onClick={() => onToggleNpcName(npcName)}
          title={npcName}
          type="button"
        >
          <CharacterPortrait
            name={npcName}
            size="sm"
            sourceModId={npcPortraitModIds?.get(npcName)}
          />
          <span>{formatNpcFilterLabel(npcName)}</span>
        </button>
      ))}
      {overflowNpcNames.length > 0 ? (
        <details className="portrait-filter-more portrait-filter-more--inline">
          <summary aria-label={`展开全部 ${npcNames.length + overflowNpcNames.length} 个角色`}>
            +
          </summary>
          <div className="portrait-filter-more__panel">
            <PortraitFilterGrid
              npcNames={overflowNpcNames}
              npcPortraitModIds={npcPortraitModIds}
              selectedNpcNames={selectedNpcNames}
              onToggleNpcName={onToggleNpcName}
            />
          </div>
        </details>
      ) : null}
    </div>
  );
}

interface FilterGroupProps {
  options: string[];
  selectedOptions: ReadonlySet<string>;
  onToggleOption: (value: string) => void;
  renderOption?: (value: string) => string;
}

function FilterGroup({
  options,
  selectedOptions,
  onToggleOption,
  renderOption,
}: FilterGroupProps) {
  return options.length === 0 ? (
    <p className="filter-empty">暂无可选项</p>
  ) : (
    <div className="filter-option-list">
      {options.map((option) => (
        <label className="checkbox-row" key={option} title={option}>
          <input
            checked={selectedOptions.has(option)}
            onChange={() => onToggleOption(option)}
            type="checkbox"
          />
          <span>{renderOption ? renderOption(option) : option}</span>
        </label>
      ))}
    </div>
  );
}
