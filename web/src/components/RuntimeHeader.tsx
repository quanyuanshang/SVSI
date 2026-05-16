import { CharacterPortrait } from "./CharacterPortrait";
import { StardewButton } from "./StardewButton";
import { StardewNineSlicePanel } from "./StardewNineSlicePanel";
import { StardewSpriteIcon } from "./StardewSpriteIcon";
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
    <StardewNineSlicePanel as="section" className="panel runtime-header" variant="darkWood">
      <div className="runtime-player-card">
        <CharacterPortrait label={runtimeState?.playerName ?? "农夫"} size="lg" />
        <div>
          <span>玩家</span>
          <strong>{runtimeState?.playerName ?? "未知玩家"}</strong>
          <small>农夫</small>
        </div>
      </div>

      <RuntimeFact label="存档时间" value={formatStardewTime(runtimeState?.time)} />
      <RuntimeFact label="日期" value={formatStardewDate(runtimeState)} wide />
      <RuntimeFact label="天气" value={formatWeatherZh(runtimeState?.weather)} />
      <RuntimeFact
        label="地点"
        value={formatLocationZh(runtimeState?.currentLocation)}
        title={runtimeState?.currentLocation ?? undefined}
      />
      <RuntimeFact
        label="游戏时长"
        value={lastLoadedAt ? lastLoadedAt.toLocaleTimeString() : "尚未加载"}
      />

      <div className="runtime-asset-smoke" aria-label="Stardew sprite resolver preview">
        <StardewSpriteIcon spriteKey="icon.scrollUp" size={22} title="scroll up" fallback="↑" />
        <StardewSpriteIcon spriteKey="icon.scrollDown" size={22} title="scroll down" fallback="↓" />
        <StardewSpriteIcon spriteKey="debug.missing" size={22} title="missing sprite fallback" fallback="?" />
        <a href="/stardew-assets-debug">资源标注</a>
      </div>

      <StardewButton
        className="action-button"
        onClick={() => {
          void onRefresh();
        }}
        type="button"
      >
        {loading ? "刷新中..." : "刷新快照"}
      </StardewButton>

      {error ? <p className="status-banner status-banner--error">{error}</p> : null}
    </StardewNineSlicePanel>
  );
}

function RuntimeFact({
  label,
  value,
  title,
  wide = false,
}: {
  label: string;
  value: string;
  title?: string;
  wide?: boolean;
}) {
  return (
    <div className={`runtime-fact${wide ? " runtime-fact--wide" : ""}`}>
      <span>{label}</span>
      <strong title={title}>{value}</strong>
    </div>
  );
}
