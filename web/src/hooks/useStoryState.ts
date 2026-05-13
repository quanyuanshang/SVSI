import { useCallback, useEffect, useRef, useState } from "react";
import type { StoryStateEvaluationReport } from "../types/story";

interface ApiErrorPayload {
  error?: string;
}

interface UseStoryStateResult {
  data: StoryStateEvaluationReport | null;
  loading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
  lastLoadedAt: Date | null;
}

const REFRESH_INTERVAL_MS = 3000;

export function useStoryState(): UseStoryStateResult {
  const [data, setData] = useState<StoryStateEvaluationReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastLoadedAt, setLastLoadedAt] = useState<Date | null>(null);
  const isMountedRef = useRef(true);

  const refresh = useCallback(async () => {
    try {
      const response = await fetch("/api/story-state", { cache: "no-store" });

      if (!response.ok) {
        let message = `Failed to load story state (${response.status})`;

        try {
          const payload = (await response.json()) as ApiErrorPayload;
          if (payload.error) {
            message = payload.error;
          }
        } catch {
          // Ignore JSON parsing errors and keep the default message.
        }

        throw new Error(message);
      }

      const nextData = (await response.json()) as StoryStateEvaluationReport;
      if (!isMountedRef.current) {
        return;
      }

      setData(nextData);
      setError(null);
      setLastLoadedAt(new Date());
    } catch (refreshError) {
      if (!isMountedRef.current) {
        return;
      }

      setError(
        refreshError instanceof Error ? refreshError.message : "Unknown error",
      );
    } finally {
      if (isMountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    isMountedRef.current = true;
    void refresh();

    const intervalId = window.setInterval(() => {
      void refresh();
    }, REFRESH_INTERVAL_MS);

    return () => {
      isMountedRef.current = false;
      window.clearInterval(intervalId);
    };
  }, [refresh]);

  return {
    data,
    loading,
    error,
    refresh,
    lastLoadedAt,
  };
}
