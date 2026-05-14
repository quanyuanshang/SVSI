import {
  formatLocationZh,
  formatStardewDate,
  formatStardewTime,
  formatWeatherZh,
} from "../lib/format";
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
          <p className="eyebrow">运行时快照</p>
          <h1>Stardew Story Inspector</h1>
        </div>
        <button
          className="action-button"
          onClick={() => {
            void onRefresh();
          }}
          type="button"
        >
          {loading ? "刷新中..." : "立即刷新"}
        </button>
      </div>

      {error && <p className="status-banner status-banner--error">{error}</p>}

      <div className="runtime-grid">
        <div className="runtime-pill runtime-pill--wide">
          <span className="runtime-pill__label">玩家</span>
          <strong>{runtimeState?.playerName ?? "未知玩家"}</strong>
        </div>
        <div className="runtime-pill runtime-pill--wide">
          <span className="runtime-pill__label">日期</span>
          <strong>{formatStardewDate(runtimeState)}</strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">时间</span>
          <strong>{formatStardewTime(runtimeState?.time)}</strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">天气</span>
          <strong>{formatWeatherZh(runtimeState?.weather)}</strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">地点</span>
          <strong title={runtimeState?.currentLocation || undefined}>
            {formatLocationZh(runtimeState?.currentLocation)}
          </strong>
        </div>
        <div className="runtime-pill">
          <span className="runtime-pill__label">最后加载</span>
          <strong>
            {lastLoadedAt ? lastLoadedAt.toLocaleTimeString() : "尚未加载"}
          </strong>
        </div>
      </div>
    </section>
  );
}
