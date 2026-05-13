import { useEffect, useMemo, useState } from "react";
import { AppShell } from "./components/AppShell";
import { ConflictPanel } from "./components/ConflictPanel";
import { DayTimelineView } from "./components/DayTimelineView";
import { FilterPanel } from "./components/FilterPanel";
import { ProgressTimelineView } from "./components/ProgressTimelineView";
import { RuntimeHeader } from "./components/RuntimeHeader";
import { StoryNodeDetail } from "./components/StoryNodeDetail";
import { useEventHistory } from "./hooks/useEventHistory";
import { useStoryFilters } from "./hooks/useStoryFilters";
import { detectPotentialConflicts } from "./lib/conflictDetection";
import {
  buildHistoryNodeMap,
  enrichHistoryEntries,
  findHistoryEntriesForNode,
} from "./lib/historyLookup";
import { useStoryState } from "./hooks/useStoryState";
import type { ObservedEventHistoryEntry } from "./types/history";
import type { StoryNodeEvaluation } from "./types/story";
import "./styles.css";

type AppTab = "today" | "progress";

export default function App() {
  const { data, loading, error, refresh, lastLoadedAt } = useStoryState();
  const {
    data: history,
    loading: historyLoading,
    error: historyError,
    refresh: refreshHistory,
  } = useEventHistory();
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<AppTab>("today");

  const nodes = data?.nodes ?? [];
  const historyEntries = useMemo(
    () => enrichHistoryEntries(history?.entries ?? [], nodes),
    [history?.entries, nodes],
  );
  const {
    filters,
    filteredNodes,
    availableOptions,
    setHideTriggered,
    setSearchText,
    toggleStatus,
    toggleModName,
    toggleLocation,
    toggleNpcName,
  } = useStoryFilters(nodes);

  const selectedNode = useMemo(() => {
    if (!selectedNodeId) {
      return null;
    }

    return (
      filteredNodes.find((node) => node.nodeId === selectedNodeId) ??
      nodes.find((node) => node.nodeId === selectedNodeId) ??
      null
    );
  }, [filteredNodes, nodes, selectedNodeId]);

  useEffect(() => {
    if (!selectedNodeId) {
      return;
    }

    if (activeTab !== "today") {
      return;
    }

    const exists = filteredNodes.some((node) => node.nodeId === selectedNodeId);
    if (!exists) {
      setSelectedNodeId(null);
    }
  }, [activeTab, filteredNodes, selectedNodeId]);

  const handleSelectNode = (node: StoryNodeEvaluation) => {
    setSelectedNodeId(node.nodeId ?? null);
  };

  const nodesById = useMemo(() => {
    return buildHistoryNodeMap(nodes);
  }, [nodes]);

  const selectedHistoryEntries = useMemo(
    () => findHistoryEntriesForNode(historyEntries, selectedNode),
    [historyEntries, selectedNode],
  );

  const potentialConflicts = useMemo(
    () => detectPotentialConflicts(filteredNodes),
    [filteredNodes],
  );

  const conflictCountByNodeId = useMemo(() => {
    const counts: Record<string, number> = {};

    for (const conflict of potentialConflicts) {
      counts[conflict.nodeAId] = (counts[conflict.nodeAId] ?? 0) + 1;
      counts[conflict.nodeBId] = (counts[conflict.nodeBId] ?? 0) + 1;
    }

    return counts;
  }, [potentialConflicts]);

  const handleSelectNodeId = (nodeId: string) => {
    setSelectedNodeId(nodeId);
  };

  const handleSelectHistoryEntry = (entry: ObservedEventHistoryEntry) => {
    if (!entry.nodeId || !nodesById.has(entry.nodeId)) {
      return;
    }

    setSelectedNodeId(entry.nodeId);
  };

  const handleRefresh = async () => {
    await Promise.all([refresh(), refreshHistory()]);
  };

  const hasData = nodes.length > 0;

  if (loading && !hasData) {
    return (
      <main className="page-shell page-shell--centered">
        <p className="empty-state">Loading...</p>
      </main>
    );
  }

  return (
    <AppShell
      header={
        <RuntimeHeader
          runtimeState={data?.runtimeState}
          lastLoadedAt={lastLoadedAt}
          onRefresh={handleRefresh}
          loading={loading}
          error={error}
        />
      }
      sidebar={
        <FilterPanel
          statusCounts={data?.statusCounts}
          totalNodeCount={data?.totalNodeCount}
          filters={filters}
          availableOptions={availableOptions}
          onToggleStatus={toggleStatus}
          onToggleModName={toggleModName}
          onToggleLocation={toggleLocation}
          onToggleNpcName={toggleNpcName}
          onHideTriggeredChange={setHideTriggered}
          onSearchTextChange={setSearchText}
        />
      }
      content={
        <div className="content-stack">
          <div className="panel">
            <div className="timeline-item__tags" role="tablist" aria-label="Story view">
              <button
                className={`timeline-item__tag${activeTab === "today" ? " timeline-item--selected" : ""}`}
                onClick={() => setActiveTab("today")}
                role="tab"
                aria-selected={activeTab === "today"}
                type="button"
              >
                Today
              </button>
              <button
                className={`timeline-item__tag${activeTab === "progress" ? " timeline-item--selected" : ""}`}
                onClick={() => setActiveTab("progress")}
                role="tab"
                aria-selected={activeTab === "progress"}
                type="button"
              >
                Progress
              </button>
            </div>
          </div>

          {activeTab === "today" ? (
            <>
              <ConflictPanel
                conflicts={potentialConflicts}
                nodesById={nodesById}
                onSelectNodeId={handleSelectNodeId}
              />
              <DayTimelineView
                runtimeState={data?.runtimeState}
                nodes={filteredNodes}
                totalCount={nodes.length}
                selectedNodeId={selectedNodeId}
                conflictCountByNodeId={conflictCountByNodeId}
                onSelectNode={handleSelectNode}
              />
            </>
          ) : (
            <ProgressTimelineView
              entries={historyEntries}
              loading={historyLoading}
              error={historyError}
              selectedNodeId={selectedNodeId}
              onSelectEntry={handleSelectHistoryEntry}
            />
          )}
        </div>
      }
      detail={
        <StoryNodeDetail
          node={selectedNode}
          historyEntries={selectedHistoryEntries}
        />
      }
    />
  );
}
