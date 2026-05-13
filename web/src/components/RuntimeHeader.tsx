import { formatStardewDate, formatStardewTime } from "../lib/format";
import type { RuntimeGameState } from "../types/story";

interface RuntimeHeaderProps {
  runtimeState?: RuntimeGameState | null;
  lastLoadedAt: Date | null;
  onRefresh: () => Promise<void>;
  loading: boolean;
  error: string | null;
}

export function RuntimeHeader({
  runtimeState,
  lastLoadedAt,
  onRefresh,
  loading,
  error,
}: RuntimeHeaderProps) {
  return (
    <section className="panel runtime-header">
      <div className="runtime-header__title-row">
        <div>
          <p className="eyebrow">Runtime Snapshot</p>
          <h1>StardewStoryInspector</h1>
        </div>
        <button
          className="action-button"
          onClick={() => {
            void onRefresh();
          }}
          type="button"
        >
          {loading ? "Refreshing..." : "Refresh"}
        </button>
      </div>

      {error && <p className="status-banner status-banner--error">{error}</p>}

      <div className="runtime-grid">
        <div className="runtime-pill runtime-pill--wide">
          <span className="runtime-pill__label">Player</span>
          <strong>{runtimeState?.playerName ?? "Unknown player"}</strong>
        </div>
        <div className="runtime-pill runtime-pill--wide">
          <span className="runtime-pill__label">Date</span>
          <strong>{formatStardewDate(runtimeState)}</strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">Time</span>
          <strong>{formatStardewTime(runtimeState?.time)}</strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">Weather</span>
          <strong>{runtimeState?.weather ?? "Unknown"}</strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">Location</span>
          <strong>{runtimeState?.currentLocation ?? "Unknown"}</strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">Last Loaded</span>
          <strong>
            {lastLoadedAt ? lastLoadedAt.toLocaleTimeString() : "Not loaded"}
          </strong>
        </div>
      </div>
    </section>
  );
}
