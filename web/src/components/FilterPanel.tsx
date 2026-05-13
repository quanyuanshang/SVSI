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
        <h2>Status Counts</h2>
        <p>Use the checkboxes to narrow the story list.</p>
      </div>

      <div className="summary-stat">
        <span className="summary-stat__label">Total Nodes</span>
        <strong>{totalNodeCount ?? 0}</strong>
      </div>

      <ul className="status-count-list">
        {STATUS_ORDER.map((status) => (
          <li className="status-count-row" key={status}>
            <span>{status}</span>
            <strong>{statusCounts?.[status] ?? 0}</strong>
          </li>
        ))}
      </ul>

      <div className="filter-section">
        <label className="filter-label" htmlFor="story-search">
          Search
        </label>
        <input
          className="filter-input"
          id="story-search"
          onChange={(event) => onSearchTextChange(event.target.value)}
          placeholder="Search event, mod, location, reason..."
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
          <span>Hide Triggered</span>
        </label>
      </div>

      <FilterGroup
        label="Status"
        options={availableOptions.statuses}
        selectedOptions={filters.selectedStatuses}
        onToggleOption={(status) => onToggleStatus(status as StoryNodeStatus)}
      />
      <FilterGroup
        label="Mod"
        options={availableOptions.modNames}
        selectedOptions={filters.selectedModNames}
        onToggleOption={onToggleModName}
      />
      <FilterGroup
        label="Location"
        options={availableOptions.locations}
        selectedOptions={filters.selectedLocations}
        onToggleOption={onToggleLocation}
      />
      <FilterGroup
        label="NPC"
        options={availableOptions.npcNames}
        selectedOptions={filters.selectedNpcNames}
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
}

function FilterGroup({
  label,
  options,
  selectedOptions,
  onToggleOption,
}: FilterGroupProps) {
  return (
    <div className="filter-section">
      <p className="filter-label">{label}</p>
      {options.length === 0 ? (
        <p className="filter-empty">No options</p>
      ) : (
        <div className="filter-option-list">
          {options.map((option) => (
            <label className="checkbox-row" key={option}>
              <input
                checked={selectedOptions.has(option)}
                onChange={() => onToggleOption(option)}
                type="checkbox"
              />
              <span>{option}</span>
            </label>
          ))}
        </div>
      )}
    </div>
  );
}
