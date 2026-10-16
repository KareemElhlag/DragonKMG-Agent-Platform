import { useCallback, useEffect, useRef, useState } from "react";
import { request } from "../services/api";
export type ExecutionEvent = { phase: string; agentName: string | null; modelId: string | null; member: number; total: number };
type Snapshot = { requestId: string; updatedAt: string; events: ExecutionEvent[] };
export function useSessionExecution(sessionId: string) {
  const [events, setEvents] = useState<ExecutionEvent[]>([]);
  const [active, setActive] = useState("");
  const current = useRef("");
  const reset = useCallback(() => { current.current = ""; setActive(""); setEvents([]); }, []);
  useEffect(() => {
    if (!active) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        const { execution: value } = await request<{ execution: Snapshot | null }>(`/api/sessions/${sessionId}/execution`, { signal: AbortSignal.any([controller.signal, AbortSignal.timeout(3000)]) });
        if (!controller.signal.aborted && current.current === active && value?.requestId === active) setEvents(value.events);
      } catch { /* Operational polling must not retry the prompt or replace its error. */ }
      if (!controller.signal.aborted) timer = setTimeout(() => void poll(), 800);
    };
    void poll();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [active, sessionId]);
  return {
    events,
    begin: () => { const id = crypto.randomUUID(); current.current = id; setEvents([]); setActive(id); return id; },
    finish: async (id: string) => {
      try {
        const { execution: value } = await request<{ execution: Snapshot | null }>(`/api/sessions/${sessionId}/execution`, { signal: AbortSignal.timeout(3000) });
        if (current.current === id && value?.requestId === id) setEvents(value.events);
      } catch { /* Status is transient, saved chat remains authoritative. */ }
      if (current.current === id) setActive("");
    },
    reset,
  };
}
