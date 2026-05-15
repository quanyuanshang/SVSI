import { useMemo } from "react";
import {
  formatLocationZh,
  formatSeasonZh,
  formatStatusLabel,
  formatStatusReasonZh,
  formatTimeRangeZh,
  formatWeatherZh,
} from "../lib/format";
import { formatGameDate } from "../lib/gameDate";
import { extractCharactersFromNode } from "../lib/characters";
import { extractTimeWindow } from "../lib/timeWindows";
import {
  buildGameStateFromRuntime,
  diagnoseEventTrigger,
  formatDiagnosticZh,
  type CurrentGameState,
  type DiagnosticItem,
  type DiagnosisResult,
} from "../lib/triggerDiagnosis";
import { translateCharacter, translateLocation } from "../lib/translations";
import type { ObservedEventHistoryEntry } from "../types/history";
import type {
  ConditionAtomResult,
  RuntimeGameState,
  StoryNodeEvaluation,
} from "../types/story";

interface StoryNodeDetailProps {
  node: StoryNodeEvaluation | null;
  historyEntries?: ObservedEventHistoryEntry[];
  runtimeState?: RuntimeGameState | null;
  availableEventIds?: ReadonlySet<string>;
}

function renderAtomValue(atom: ConditionAtomResult): string {
  if (atom.passed === true) {
    return "已满足";
  }

  if (atom.passed === false) {
    return "未满足";
  }

  return "未解析";
}

function diagnosisVerdictLabel(diagnosis: DiagnosisResult): string {
  if (diagnosis.unsatisfied.length > 0) {
    return "暂不可触发";
  }

  if (diagnosis.unknown.length > 0) {
    return "条件未知";
  }

  return "可触发";
}

function diagnosisVerdictClass(diagnosis: DiagnosisResult): string {
  if (diagnosis.unsatisfied.length > 0) {
    return "diagnosis-verdict diagnosis-verdict--blocked";
  }

  if (diagnosis.unknown.length > 0) {
    return "diagnosis-verdict diagnosis-verdict--unknown";
  }

  return "diagnosis-verdict diagnosis-verdict--ready";
}

function ConditionList({
  title,
  items,
  emptyText,
  variant,
}: {
  title: string;
  items: DiagnosticItem[];
  emptyText: string;
  variant: "satisfied" | "unsatisfied" | "unknown";
}) {
  return (
    <div className={`diagnosis-group diagnosis-group--${variant}`}>
      <h4>
        {title} <span>({items.length})</span>
      </h4>
      {items.length === 0 ? (
        <p className="empty-state">{emptyText}</p>
      ) : (
        <ul className="atom-result-list">
          {items.map((item, index) => (
            <li
              key={`${variant}-${item.conditionRaw}-${index}`}
              className="atom-result-card"
            >
              <div className="atom-result-card__header">
                <strong>{item.descriptionZh}</strong>
                <span>
                  {item.status === "unknown"
                    ? "警告"
                    : item.status === "satisfied"
                      ? "满足"
                      : "未满足"}
                </span>
              </div>
              <p className="atom-result-card__reason">{formatDiagnosticZh(item)}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export function StoryNodeDetail({
  node,
  historyEntries = [],
  runtimeState,
  availableEventIds,
}: StoryNodeDetailProps) {
  const gameState = useMemo<CurrentGameState>(
    () => buildGameStateFromRuntime(runtimeState),
    [runtimeState],
  );

  const diagnosis = useMemo<DiagnosisResult | null>(
    () =>
      node
        ? diagnoseEventTrigger(node, gameState, { availableEventIds })
        : null,
    [node, gameState, availableEventIds],
  );

  const characters = useMemo<string[]>(
    () => (node ? extractCharactersFromNode(node) : []),
    [node],
  );

  if (!node) {
    return (
      <section className="panel story-node-detail story-node-detail--empty">
        <p className="empty-state">请选择一个事件查看详情。</p>
      </section>
    );
  }

  const atomResults = node.conditionResult?.atomResults ?? [];
  const timeWindow = extractTimeWindow(node);
  const seasonAtom = node.rawPreconditions?.find((item) => /^(s|z|Season)\b/i.test(item));
  const weatherAtom = node.rawPreconditions?.find((item) => /^!?w\b/i.test(item));
  const locationTranslation = translateLocation(node.location, node.sourceModId);

  return (
    <section className="panel story-node-detail">
      <div className="panel-heading">
        <h2>事件详情</h2>
        <p>{node.nodeId ?? "未知节点"}</p>
      </div>

      <dl className="detail-grid">
        <div>
          <dt>事件</dt>
          <dd>{node.eventId ?? "未知事件"}</dd>
        </div>
        <div>
          <dt>来源 Mod</dt>
          <dd>{node.sourceModName ?? "未知 Mod"}</dd>
        </div>
        <div>
          <dt>触发地点</dt>
          <dd title={node.location ?? undefined}>{locationTranslation.zh}</dd>
        </div>
        <div>
          <dt>触发状态</dt>
          <dd>{formatStatusLabel(node.status)}</dd>
        </div>
        <div>
          <dt>触发时间</dt>
          <dd>
            {timeWindow
              ? formatTimeRangeZh(timeWindow.start, timeWindow.end)
              : "任意时间"}
          </dd>
        </div>
        <div>
          <dt>触发季节</dt>
          <dd>
            {seasonAtom
              ? seasonAtom
                  .split(/\s+/)
                  .slice(1)
                  .map((item) => formatSeasonZh(item))
                  .join(" / ") || "任意季节"
              : "任意季节"}
          </dd>
        </div>
        <div>
          <dt>触发天气</dt>
          <dd>
            {weatherAtom
              ? formatWeatherZh(weatherAtom.split(/\s+/).slice(1)[0])
              : "任意天气"}
          </dd>
        </div>
      </dl>

      <div className="detail-block">
        <h3>出现角色</h3>
        {characters.length === 0 ? (
          <p className="empty-state">暂未从当前事件中识别到角色。</p>
        ) : (
          <ul className="character-chip-list">
            {characters.map((name) => {
              const translation = translateCharacter(name, node.sourceModId);
              return (
                <li className="character-chip" key={name} title={name}>
                  <span className="character-chip__zh">{translation.zh}</span>
                </li>
              );
            })}
          </ul>
        )}
      </div>

      {diagnosis ? (
        <div className="detail-block">
          <h3>触发诊断</h3>
          <p className={diagnosisVerdictClass(diagnosis)}>
            {diagnosisVerdictLabel(diagnosis)}
            <span>
              已满足 {diagnosis.satisfied.length} / 未满足 {diagnosis.unsatisfied.length} / 未解析 {diagnosis.unknown.length}
            </span>
          </p>
          <ConditionList
            title="已满足条件"
            items={diagnosis.satisfied}
            emptyText="当前没有已满足条件。"
            variant="satisfied"
          />
          <ConditionList
            title="未满足条件"
            items={diagnosis.unsatisfied}
            emptyText="所有可评估条件都已满足。"
            variant="unsatisfied"
          />
          <ConditionList
            title="未解析条件"
            items={diagnosis.unknown}
            emptyText="没有未解析条件。"
            variant="unknown"
          />
        </div>
      ) : null}

      <div className="detail-block">
        <h3>调试原因</h3>
        <p>{formatStatusReasonZh(node.statusReason, node)}</p>
      </div>

      <div className="detail-block">
        <h3>本地事件历史</h3>
        {historyEntries.length === 0 ? (
          <p className="empty-state">这个事件还没有本地历史记录。</p>
        ) : (
          <ul className="atom-result-list">
            {historyEntries.map((entry) => (
              <li
                className="atom-result-card"
                key={`${entry.eventId}-${entry.observedAtUtc ?? ""}`}
              >
                <div className="atom-result-card__header">
                  <strong>{entry.eventId || "未知事件"}</strong>
                  <span>{entry.observationSource || "事件历史"}</span>
                </div>
                <p>首次看到：{formatGameDate(entry.firstSeenGameDate ?? entry.date)}</p>
                <p className="atom-result-card__reason">
                  地点：{formatLocationZh(entry.location)}
                </p>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="detail-block">
        <h3>原始数据 Debug</h3>
        <details>
          <summary>展开 Debug</summary>
          <div className="condition-summary condition-summary--stacked">
            <span>Raw key：{node.rawKey ?? node.eventId ?? "未知"}</span>
            <span>Raw location：{node.location ?? "无"}</span>
            <span>Location source：{locationTranslation.source}</span>
            {locationTranslation.confidence ? <span>Location confidence：{locationTranslation.confidence}</span> : null}
            {locationTranslation.sourcePath ? <span>Location source path：{locationTranslation.sourcePath}</span> : null}
            {locationTranslation.note ? <span>Location note：{locationTranslation.note}</span> : null}
            <span>
              Raw preconditions：{node.rawPreconditions?.length ? node.rawPreconditions.join(" / ") : "无"}
            </span>
            <span>
              Unknown fragments：{node.unknownFragments?.length ? node.unknownFragments.join(" / ") : "无"}
            </span>
            {node.statusReason ? <span>Raw status reason：{node.statusReason}</span> : null}
            {node.rawScriptPreview ? (
              <span>Raw script：{node.rawScriptPreview.slice(0, 200)}</span>
            ) : null}
          </div>

          {characters.length > 0 ? (
            <ul className="atom-result-list">
              {characters.map((name) => {
                const translation = translateCharacter(name, node.sourceModId);
                return (
                  <li className="atom-result-card" key={`debug-char-${name}`}>
                    <div className="atom-result-card__header">
                      <strong>{translation.zh}</strong>
                      <span>{translation.source}</span>
                    </div>
                    <p className="atom-result-card__raw">Raw NPC：{name}</p>
                    {translation.sourcePath ? (
                      <p className="atom-result-card__raw">Source path：{translation.sourcePath}</p>
                    ) : null}
                  </li>
                );
              })}
            </ul>
          ) : null}

          {diagnosis ? (
            <ul className="atom-result-list">
              {[...diagnosis.satisfied, ...diagnosis.unsatisfied, ...diagnosis.unknown].map((item, index) => (
                <li className="atom-result-card" key={`debug-diag-${index}-${item.conditionRaw}`}>
                  <div className="atom-result-card__header">
                    <strong>{item.descriptionZh}</strong>
                    <span>{item.status}</span>
                  </div>
                  <p className="atom-result-card__reason">{item.reasonZh}</p>
                  <p className="atom-result-card__raw">Condition raw：{item.conditionRaw}</p>
                  {item.reasonRaw ? (
                    <p className="atom-result-card__raw">Reason raw：{item.reasonRaw}</p>
                  ) : null}
                </li>
              ))}
            </ul>
          ) : null}

          {node.patchWhenConditions?.length ? (
            <ul className="atom-result-list">
              {node.patchWhenConditions.map((condition, index) => (
                <li className="atom-result-card" key={`${condition.key ?? "when"}-${index}`}>
                  <div className="atom-result-card__header">
                    <strong>{condition.key ?? "未知键"}</strong>
                    <span>
                      {condition.isKnown
                        ? "已评估"
                        : condition.unknownKind === "runtimeMissing"
                          ? "无法判断"
                          : condition.unknownKind === "complexQueryUnsupported"
                            || condition.unknownKind === "randomTokenUnsupported"
                            ? "随机/概率"
                            : condition.unknownKind === "externalTokenMissing"
                              ? "外部 token"
                              : "未解析"}
                    </span>
                  </div>
                  <p className="atom-result-card__reason">
                    {condition.isKnown
                      ? condition.reasonZh ?? condition.reason ?? "已评估。"
                      : condition.unknownKind === "runtimeMissing"
                        ? (condition.reasonZh?.startsWith("无法判断：")
                            ? condition.reasonZh
                            : `无法判断：${condition.reasonZh ?? condition.reason ?? "运行时状态缺失"}`)
                        : condition.unknownKind === "complexQueryUnsupported"
                          || condition.unknownKind === "randomTokenUnsupported"
                          ? (condition.reasonZh ?? "随机/概率条件暂不展开。")
                          : condition.unknownKind === "externalTokenMissing"
                            ? (condition.reasonZh ?? condition.reason ?? "外部条件未导出。")
                            : `未解析条件：${condition.key ?? "When"}`}
                  </p>
                  <p className="atom-result-card__raw">
                    Raw value：{condition.value ?? condition.rawValue ?? "无原始值"}
                  </p>
                </li>
              ))}
            </ul>
          ) : null}

          {atomResults.length > 0 ? (
            <ul className="atom-result-list">
              {atomResults.map((atom, index) => (
                <li className="atom-result-card" key={`${atom.raw ?? atom.atomType}-${index}`}>
                  <div className="atom-result-card__header">
                    <strong>{atom.atomType ?? "未知条件"}</strong>
                    <span>{renderAtomValue(atom)}</span>
                  </div>
                  <p className="atom-result-card__reason">
                    {atom.passed === null || atom.passed === undefined
                      ? (atom.reasonZh ?? atom.reason ?? "暂无说明。")
                      : (atom.reason ?? atom.reasonZh ?? "暂无说明。")}
                  </p>
                  <p className="atom-result-card__raw">{atom.raw ?? "无原始条件"}</p>
                </li>
              ))}
            </ul>
          ) : null}
        </details>
      </div>
    </section>
  );
}
