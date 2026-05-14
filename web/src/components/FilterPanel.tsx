import { formatStatusLabel } from "../lib/format";
import { translateCharacter, translateLocation } from "../lib/translations";
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
}

const STATUS_ORDER: StoryNodeStatus[] = [
  "Current",
  "AvailableLater",
  "Locked",
  "Unknown",
  "Triggered",
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
}: FilterPanelProps) {
  return (
    <aside className="panel filter-panel">
      <div className="panel-heading">
        <h2>筛选条件</h2>
        <p>默认按中文展示，raw 名称保留在提示里。</p>
      </div>

      <div className="summary-stat">
        <span className="summary-stat__label">事件总数</span>
        <strong>{totalNodeCount ?? 0}</strong>
      </div>

      <ul className="status-count-list">
        {STATUS_ORDER.map((status) => (
          <li className="status-count-row" key={status}>
            <span>{formatStatusLabel(status)}</span>
            <strong>{statusCounts?.[status] ?? 0}</strong>
          </li>
        ))}
      </ul>

      <div className="filter-section">
        <label className="filter-label" htmlFor="story-search">
          搜索
        </label>
        <input
          className="filter-input"
          id="story-search"
          onChange={(event) => onSearchTextChange(event.target.value)}
          placeholder="搜索事件、Mod、地点、角色、原因..."
          type="search"
          value={filters.searchText}
        />
      </div>

      <div className="filter-section">
        <label className="checkbox-row">
          <input
            checked={filters.hideTriggered}
            onChange={(event) => onHideTriggeredChange(event.target.checked)}
            type="checkbox"
          />
          <span>隐藏已触发事件</span>
        </label>
      </div>

      <FilterGroup
        label="触发状态"
        options={availableOptions.statuses}
        selectedOptions={filters.selectedStatuses}
        renderOption={(value) => formatStatusLabel(value as StoryNodeStatus)}
        onToggleOption={(status) => onToggleStatus(status as StoryNodeStatus)}
      />
      <FilterGroup
        label="来源 Mod"
        options={availableOptions.modNames}
        selectedOptions={filters.selectedModNames}
        onToggleOption={onToggleModName}
      />
      <FilterGroup
        label="地点"
        options={availableOptions.locations}
        selectedOptions={filters.selectedLocations}
        renderOption={(value) => translateLocation(value).zh}
        onToggleOption={onToggleLocation}
      />
      <FilterGroup
        label="角色"
        options={availableOptions.npcNames}
        selectedOptions={filters.selectedNpcNames}
        renderOption={(value) => translateCharacter(value).zh}
        onToggleOption={onToggleNpcName}
      />
    </aside>
  );
}

interface FilterGroupProps {
  label: string;
  options: string[];
  selectedOptions: ReadonlySet<string>;
  onToggleOption: (value: string) => void;
  renderOption?: (value: string) => string;
}

function FilterGroup({
  label,
  options,
  selectedOptions,
  onToggleOption,
  renderOption,
}: FilterGroupProps) {
  return (
    <div className="filter-section">
      <p className="filter-label">{label}</p>
      {options.length === 0 ? (
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
      )}
    </div>
  );
}
