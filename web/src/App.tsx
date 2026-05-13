import { useEffect, useMemo, useState } from "react";
import { AppShell } from "./components/AppShell";
import { ConflictPanel } from "./components/ConflictPanel";
import { DayTimelineView } from "./components/DayTimelineView";
import { FilterPanel } from "./components/FilterPanel";
import { RuntimeHeader } from "./components/RuntimeHeader";
import { StoryNodeDetail } from "./components/StoryNodeDetail";
import { useStoryFilters } from "./hooks/useStoryFilters";
import { detectPotentialConflicts } from "./lib/conflictDetection";
import { useStoryState } from "./hooks/useStoryState";
import type { StoryNodeEvaluation } from "./types/story";
import "./styles.css";

export default function App() {
  const { data, loading, error, refresh, lastLoadedAt } = useStoryState();
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);

  const nodes = data?.nodes ?? [];
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

    const exists = filteredNodes.some((node) => node.nodeId === selectedNodeId);
    if (!exists) {
      setSelectedNodeId(null);
    }
  }, [filteredNodes, selectedNodeId]);

  const handleSelectNode = (node: StoryNodeEvaluation) => {
    setSelectedNodeId(node.nodeId ?? null);
  };

  const nodesById = useMemo(() => {
    const map = new Map<string, StoryNodeEvaluation>();

    for (const node of nodes) {
      const nodeId = node.nodeId;
      if (!nodeId) {
        continue;
      }

      map.set(nodeId, node);
    }

    return map;
  }, [nodes]);

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
          onRefresh={refresh}
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
        </div>
      }
      detail={<StoryNodeDetail node={selectedNode} />}
    />
  );
}
