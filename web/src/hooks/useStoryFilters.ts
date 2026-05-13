import { useMemo, useState } from "react";
import { applyStoryFilters, getAvailableFilterOptions } from "../lib/storyFilters";
import type {
  StoryFilterOptions,
  StoryFilterState,
  StoryNodeEvaluation,
  StoryNodeStatus,
} from "../types/story";

interface UseStoryFiltersResult {
  filters: StoryFilterState;
  filteredNodes: StoryNodeEvaluation[];
  availableOptions: StoryFilterOptions;
  setHideTriggered: (value: boolean) => void;
  setSearchText: (value: string) => void;
  toggleStatus: (status: StoryNodeStatus) => void;
  toggleModName: (modName: string) => void;
  toggleLocation: (location: string) => void;
  toggleNpcName: (npcName: string) => void;
}

const INITIAL_FILTERS: StoryFilterState = {
  selectedStatuses: new Set<StoryNodeStatus>(),
  selectedModNames: new Set<string>(),
  selectedLocations: new Set<string>(),
  selectedNpcNames: new Set<string>(),
  hideTriggered: false,
  searchText: "",
};

export function useStoryFilters(
  nodes: StoryNodeEvaluation[],
): UseStoryFiltersResult {
  const [filters, setFilters] = useState<StoryFilterState>(INITIAL_FILTERS);

  const availableOptions = useMemo(
    () => getAvailableFilterOptions(nodes),
    [nodes],
  );

  const filteredNodes = useMemo(
    () => applyStoryFilters(nodes, filters),
    [nodes, filters],
  );

  const toggleSetValue = <T,>(
    currentSet: Set<T>,
    value: T,
  ): Set<T> => {
    const nextSet = new Set(currentSet);
    if (nextSet.has(value)) {
      nextSet.delete(value);
    } else {
      nextSet.add(value);
    }

    return nextSet;
  };

  return {
    filters,
    filteredNodes,
    availableOptions,
    setHideTriggered: (value) => {
      setFilters((current) => ({ ...current, hideTriggered: value }));
    },
    setSearchText: (value) => {
      setFilters((current) => ({ ...current, searchText: value }));
    },
    toggleStatus: (status) => {
      setFilters((current) => ({
        ...current,
        selectedStatuses: toggleSetValue(current.selectedStatuses, status),
      }));
    },
    toggleModName: (modName) => {
      setFilters((current) => ({
        ...current,
        selectedModNames: toggleSetValue(current.selectedModNames, modName),
      }));
    },
    toggleLocation: (location) => {
      setFilters((current) => ({
        ...current,
        selectedLocations: toggleSetValue(current.selectedLocations, location),
      }));
    },
    toggleNpcName: (npcName) => {
      setFilters((current) => ({
        ...current,
        selectedNpcNames: toggleSetValue(current.selectedNpcNames, npcName),
      }));
    },
  };
}
