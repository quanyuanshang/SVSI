import { useMemo, useState } from "react";
import { ActionBoard } from "./components/ActionBoard";
import { AppShell } from "./components/AppShell";
import { EventDetailView } from "./components/EventDetailView";
import { FilterPanel } from "./components/FilterPanel";
import { RuntimeHeader } from "./components/RuntimeHeader";
import { StardewAssetsDebugPage } from "./components/StardewAssetsDebugPage";
import { StorylineOverview } from "./components/StorylineOverview";
import { useEventHistory } from "./hooks/useEventHistory";
import { useStoryFilters } from "./hooks/useStoryFilters";
import {
  enrichHistoryEntries,
} from "./lib/historyLookup";
import {
  buildStoryGraph,
  findStoryNodeBySource,
  type StoryEventNode,
} from "./lib/storyGraph";
import { useStoryState } from "./hooks/useStoryState";
import "./styles.css";

export default function App() {
  if (window.location.pathname.startsWith("/stardew-assets-debug")) {
    return <StardewAssetsDebugPage />;
  }

  return <InspectorApp />;
}

function InspectorApp() {
  const { data, loading, error, refresh, lastLoadedAt } = useStoryState();
  const { data: history, refresh: refreshHistory } = useEventHistory();
  const [selectedNodeKey, setSelectedNodeKey] = useState<string | null>(null);

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
    clearFilters,
  } = useStoryFilters(nodes, data?.translationCatalog, data?.runtimeState);

  const storyGraph = useMemo(
    () => buildStoryGraph(nodes, historyEntries, {}, data?.runtimeState),
    [nodes, historyEntries, data?.runtimeState],
  );

  const filteredStoryNodes = useMemo(
    () =>
      filteredNodes
        .map((node) => findStoryNodeBySource(storyGraph, node))
        .filter((node): node is StoryEventNode => Boolean(node)),
    [filteredNodes, storyGraph],
  );

  const selectedStoryNode = selectedNodeKey
    ? storyGraph.nodesByKey.get(selectedNodeKey) ?? null
    : null;

  const availableEventIds = useMemo<ReadonlySet<string>>(() => {
    const set = new Set<string>();
    for (const node of nodes) {
      if (node.eventId) {
        set.add(node.eventId);
      }
    }
    return set;
  }, [nodes]);

  const selectedCharacter =
    filters.selectedNpcNames.size === 1
      ? Array.from(filters.selectedNpcNames)[0]
      : null;

  const handleSelectStoryNode = (node: StoryEventNode) => {
    setSelectedNodeKey(node.key);
  };

  const handleRefresh = async () => {
    await Promise.all([refresh(), refreshHistory()]);
  };

  const hasData = nodes.length > 0;

  if (loading && !hasData) {
    return (
      <main className="page-shell page-shell--centered">
        <p className="empty-state">加载中...</p>
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
          onClearFilters={clearFilters}
        />
      }
      content={
        selectedStoryNode ? (
          <EventDetailView
            graph={storyGraph}
            node={selectedStoryNode}
            runtimeState={data?.runtimeState}
            availableEventIds={availableEventIds}
            onBack={() => setSelectedNodeKey(null)}
            onSelectNode={handleSelectStoryNode}
          />
        ) : selectedCharacter ? (
          <StorylineOverview
            graph={storyGraph}
            scopedNodes={filteredStoryNodes}
            characterName={selectedCharacter}
            onSelectNode={handleSelectStoryNode}
          />
        ) : (
          <ActionBoard
            runtimeState={data?.runtimeState}
            nodes={filteredStoryNodes}
            totalCount={nodes.length}
            onSelectNode={handleSelectStoryNode}
          />
        )
      }
    />
  );
}
