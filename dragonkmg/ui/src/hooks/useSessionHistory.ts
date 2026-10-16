import { useCallback, useEffect, useRef, useState } from "react";
import type { SessionMessage } from "../models";
import { request } from "../services/api";

type HistoryPage = { messages: SessionMessage[]; hasMore: boolean; nextCursor: string | null };
type HistoryState = HistoryPage & { sessionId: string };
const empty = (sessionId: string): HistoryState => ({ sessionId, messages: [], hasMore: false, nextCursor: null });

export function useSessionHistory(sessionId: string) {
  const [state, setState] = useState<HistoryState>(() => empty(""));
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const generation = useRef(0);
  const olderLock = useRef(false);
  const invalidate = useCallback(() => { generation.current++; }, []);
  const refresh = useCallback(async () => {
    const current = ++generation.current;
    setFailed(false); setLoading(true);
    try {
      const page = sessionId ? await request<HistoryPage>(`/api/sessions/${sessionId}/history?limit=100`) : empty("");
      if (generation.current === current) setState({ ...page, sessionId });
    } catch { if (generation.current === current) setFailed(true); }
    finally { if (generation.current === current) setLoading(false); }
  }, [sessionId]);
  useEffect(() => {
    let active = true;
    void Promise.resolve().then(() => { if (active) void refresh(); });
    return () => { active = false; invalidate(); };
  }, [refresh, invalidate]);
  const loadOlder = async () => {
    if (olderLock.current || loading || state.sessionId !== sessionId || !state.hasMore || !state.nextCursor) return;
    const current = generation.current;
    olderLock.current = true; setLoadingOlder(true); setFailed(false);
    try {
      const page = await request<HistoryPage>(`/api/sessions/${sessionId}/history?limit=100&before=${encodeURIComponent(state.nextCursor)}`);
      if (current === generation.current) setState(previous => {
        const existing = new Set(previous.messages.map(message => message.id));
        return { ...page, sessionId, messages: [...page.messages.filter(message => !existing.has(message.id)), ...previous.messages] };
      });
    } catch { if (current === generation.current) setFailed(true); }
    finally { olderLock.current = false; setLoadingOlder(false); }
  };
  return {
    messages: state.sessionId === sessionId ? state.messages : [],
    loading: loading || (!failed && state.sessionId !== sessionId),
    failed, hasMore: state.sessionId === sessionId && state.hasMore,
    loadingOlder, refresh, loadOlder, invalidate,
  };
}
