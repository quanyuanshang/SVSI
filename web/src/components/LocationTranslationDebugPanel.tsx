import { getLocationDebugRows } from "../lib/translations";
import type { TranslationCatalog } from "../types/story";

interface LocationTranslationDebugPanelProps {
  translationCatalog?: TranslationCatalog | null;
}

export function LocationTranslationDebugPanel({
  translationCatalog,
}: LocationTranslationDebugPanelProps) {
  const rows = getLocationDebugRows(translationCatalog);

  return (
    <section className="panel">
      <details className="location-debug">
        <summary>地点映射 Debug 列表</summary>
        <div className="location-debug__meta">
          <span>当前共 {rows.length} 条地点映射。</span>
          <span>普通玩家界面默认不显示这些 raw/source 信息。</span>
        </div>
        <div className="location-debug__table-wrap">
          <table className="location-debug__table">
            <thead>
              <tr>
                <th>Raw</th>
                <th>中文名</th>
                <th>英文名</th>
                <th>来源 Mod</th>
                <th>来源文件</th>
                <th>来源</th>
                <th>可信度</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={`${row.raw}|${row.sourceMod}|${row.sourceFile}`}>
                  <td>{row.raw}</td>
                  <td>{row.zh}</td>
                  <td>{row.en}</td>
                  <td>{row.sourceMod}</td>
                  <td>{row.sourceFile}</td>
                  <td>{row.sourceType}</td>
                  <td>
                    {row.confidence}
                    {row.note ? <div className="location-debug__note">{row.note}</div> : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </section>
  );
}
