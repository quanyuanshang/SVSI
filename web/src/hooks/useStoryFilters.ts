import { useMemo, useState } from "react";
import { applyStoryFilters, getAvailableFilterOptions } from "../lib/storyFilters";
import { loadTranslationCatalog } from "../lib/translations";
import type {
  StoryFilterOptions,
  StoryFilterState,
  StoryNodeEvaluation,
  StoryNodeStatus,
  TranslationCatalog,
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
  translationCatalog?: TranslationCatalog | null,
): UseStoryFiltersResult {
  const [filters, setFilters] = useState<StoryFilterState>(INITIAL_FILTERS);

  // Make catalog loading part of the same memoized step that builds the filter
  // option list. Without this, the first render computes
  // `getAvailableFilterOptions` against the empty / stale module-level catalog
  // (the App-level `useEffect` that calls loadTranslationCatalog only runs
  // AFTER render), so the zh-dedup equivalence map collapses nothing and
  // toggling a checkbox only matches the exact representative raw. The memo
  // is idempotent because loadTranslationCatalog just stashes the catalog ref.
  const availableOptions = useMemo(
    () => {
      loadTranslationCatalog(translationCatalog ?? null);
      return getAvailableFilterOptions(nodes);
    },
    [nodes, translationCatalog],
  );

  const filteredNodes = useMemo(
    () =>
      applyStoryFilters(nodes, filters, {
        locationEquivalents: availableOptions.locationEquivalents,
        npcEquivalents: availableOptions.npcEquivalents,
      }),
    [nodes, filters, availableOptions],
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
