import { useCallback, useEffect, useRef, useState } from "react";
import type { DashboardController } from "./useDashboard";
import type { SessionMessage, TerminalSession } from "../models";
import type { TranslationKey } from "../i18n";
import { ApiError, providerErrorKey, request } from "../services/api";

function sessionErrorKey(cause: unknown): TranslationKey {
  if (cause instanceof ApiError && cause.code === "SessionBusy") return "This session is already running a request. Wait, then refresh history.";
  if (cause instanceof ApiError && cause.code === "SessionUnavailable") return "This session is unavailable. Select another session or create a new one.";
  return providerErrorKey(cause);
}

export function useSessionChat(dashboard: DashboardController) {
  const [sessionId, setSessionId] = useState(() => {
    const value = new URLSearchParams(location.search).get("session") ?? "";
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) ? value : "";
  });
  const [messages, setMessages] = useState<SessionMessage[]>([]);
  const [agentId, setAgentId] = useState("");
  const [draft, setDraft] = useState("");
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<TranslationKey | "">("");
  const controller = useRef<AbortController | null>(null);
  const generation = useRef(0);
  const lock = useRef(false);
  const session = dashboard.sessions.find(value => value.id === sessionId);
  const effectiveAgentId = agentId || session?.selectedAgentId || dashboard.agents.find(value => value.enabled)?.id || "";
  const refresh = useCallback(async () => {
    const current = ++generation.current;
    if (!sessionId) { setMessages([]); setLoading(false); return; }
    setLoading(true); setError("");
    try {
      const values = await request<SessionMessage[]>(`/api/sessions/${sessionId}/messages`);
      if (current === generation.current) setMessages([...values].sort((left, right) => Date.parse(left.createdAt) - Date.parse(right.createdAt)));
    } catch (cause) { if (current === generation.current) setError(sessionErrorKey(cause)); }
    finally { if (current === generation.current) setLoading(false); }
  }, [sessionId]);
  const invalidate = useCallback(() => { generation.current++; }, []);
  useEffect(() => { let active = true; void Promise.resolve().then(() => { if (active) void refresh(); }); return () => { active = false; invalidate(); }; }, [refresh, invalidate]);
  useEffect(() => () => controller.current?.abort(), []);
  const selectSession = (value: string) => {
    if (lock.current) return;
    setMessages([]); setSessionId(value); setAgentId(""); setDraft("");
    const url = new URL(location.href);
    if (value) url.searchParams.set("session", value); else url.searchParams.delete("session");
    history.replaceState(null, "", url);
  };
  const create = async (name: string) => {
    if (lock.current || !name.trim()) return;
    lock.current = true; setBusy(true); setError("");
    try {
      const now = new Date().toISOString();
      const value = await request<TerminalSession>("/api/sessions", { method: "POST", body: JSON.stringify({ id: crypto.randomUUID(), name: name.trim(), selectedAgentId: effectiveAgentId || null, createdAt: now, lastOpenedAt: now }) });
      await dashboard.load();
      lock.current = false; selectSession(value.id);
    } catch (cause) { setError(sessionErrorKey(cause)); }
    finally { lock.current = false; setBusy(false); }
  };
  const send = async () => {
    if (lock.current || loading || !sessionId || !effectiveAgentId || !draft.trim()) return;
    lock.current = true; setBusy(true); setError("");
    const pending = new AbortController(); controller.current = pending;
    setSending(true);
    try {
      await request<{ runId: string; output: string }>(`/api/sessions/${sessionId}/messages`, { method: "POST", signal: pending.signal, body: JSON.stringify({ agentId: effectiveAgentId, input: draft }) });
      setDraft(""); await refresh();
    } catch (cause) {
      const failure = pending.signal.aborted ? "Request stopped. Refresh history before sending again." : sessionErrorKey(cause);
      setError(failure);
      if (!pending.signal.aborted) {
        try { const saved = await request<SessionMessage[]>(`/api/sessions/${sessionId}/messages`); setMessages([...saved].sort((left, right) => Date.parse(left.createdAt) - Date.parse(right.createdAt))); }
        catch { /* Preserve the original failure and the unsent draft; never retry a send. */ }
      }
    }
    finally { controller.current = null; lock.current = false; setBusy(false); setSending(false); }
  };
  return { sessionId, messages, agentId: effectiveAgentId, setAgentId, draft, setDraft, loading, busy, sending, error, selectSession, create, send, refresh, cancel: () => controller.current?.abort() };
}
