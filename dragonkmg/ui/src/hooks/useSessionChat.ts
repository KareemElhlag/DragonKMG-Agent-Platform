import { useCallback, useEffect, useRef, useState } from "react";
import type { DashboardController } from "./useDashboard";
import type { TerminalSession } from "../models";
import type { TranslationKey } from "../i18n";
import { ApiError, request } from "../services/api";
import { useTeams } from "./useTeams";
import { useSessionHistory } from "./useSessionHistory";
import { useSessionProjects } from "./useSessionProjects";
import { initialSessionSelection, rememberSession } from "../services/sessionSelection";
import { fileErrorKey, useFileContext } from "./useFileContext";
import { useSessionExecution } from "./useSessionExecution";

const drafts = new Map<string, string>();

function sessionErrorKey(cause: unknown): TranslationKey {
  if (cause instanceof ApiError && cause.code === "TeamRequired") return "Choose a team for this request or save a default team for this session.";
  if (cause instanceof ApiError && cause.code === "TeamUnavailable") return "This team is unavailable. Select an enabled team.";
  if (cause instanceof ApiError && cause.code === "SessionBusy") return "This session is already running a request. Wait, then refresh history.";
  if (cause instanceof ApiError && cause.code === "SessionUnavailable") return "This session is unavailable. Select another session or create a new one.";
  return fileErrorKey(cause);
}

export function useSessionChat(dashboard: DashboardController) {
  const [sessionId, setSessionId] = useState(initialSessionSelection);
  const savedHistory = useSessionHistory(sessionId);
  const execution = useSessionExecution(sessionId);
  const resetExecution = execution.reset;
  const invalidateHistory = savedHistory.invalidate;
  const projectData = useSessionProjects(sessionId);
  const teamData = useTeams();
  const [teamId, setTeamId] = useState("");
  const [defaultTeamId, setDefaultTeamId] = useState("");
  const [teamSaved, setTeamSaved] = useState(false);
  const [draft, updateDraft] = useState(() => drafts.get(sessionId) ?? "");
  const setDraft = (value: string) => { fileData.resetFileConsent(); drafts.set(sessionId, value); updateDraft(value); };
  const [busy, setBusy] = useState(false);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<TranslationKey | "">("");
  const controller = useRef<AbortController | null>(null);
  const generation = useRef(0);
  const lock = useRef(false);
  const fileData = useFileContext({ sessionId, teamId, defaultTeamId, projectId: projectData.projectId, draft, setDraft, busy });
  const refreshTeam = useCallback(async () => {
    const current = ++generation.current;
    if (!sessionId) { setDefaultTeamId(""); return; }
    try {
      const savedTeam = await request<{ teamId: string | null }>(`/api/sessions/${sessionId}/team`);
      if (current === generation.current) setDefaultTeamId(savedTeam.teamId ?? "");
    } catch (cause) { if (current === generation.current) setError(sessionErrorKey(cause)); }
  }, [sessionId, setError]);
  const invalidate = useCallback(() => { generation.current++; }, []);
  useEffect(() => { let active = true; void Promise.resolve().then(() => { if (active) void refreshTeam(); }); return () => { active = false; invalidate(); }; }, [refreshTeam, invalidate]);
  useEffect(() => () => controller.current?.abort(), []);
  useEffect(() => { if (sessionId) rememberSession(sessionId); }, [sessionId]);
  useEffect(() => {
    const navigate = () => {
      if (lock.current || projectData.projectBusy) { rememberSession(sessionId); return; }
      const next = initialSessionSelection(); resetExecution();
      invalidate(); invalidateHistory(); setSessionId(next); setTeamId(""); setDefaultTeamId(""); setTeamSaved(false); setError(""); updateDraft(drafts.get(next) ?? "");
    };
    window.addEventListener("popstate", navigate);
    return () => window.removeEventListener("popstate", navigate);
  }, [sessionId, invalidate, invalidateHistory, projectData.projectBusy, resetExecution]);
  const refresh = async () => { setError(""); await Promise.all([savedHistory.refresh(), refreshTeam(), teamData.refresh(), projectData.refreshProjects()]); };
  const selectSession = (value: string) => {
    if (lock.current || projectData.projectBusy || fileData.fileBusy || value === sessionId) return;
    fileData.resetFileConsent();
    execution.reset();
    invalidate(); invalidateHistory(); setSessionId(value); setTeamId(""); setDefaultTeamId(""); setTeamSaved(false); setError(""); updateDraft(drafts.get(value) ?? "");
    rememberSession(value, true);
  };
  const create = async (name: string) => {
    if (lock.current || !name.trim()) return;
    lock.current = true; setBusy(true); setError("");
    try {
      const now = new Date().toISOString();
      const value = await request<TerminalSession>("/api/sessions", { method: "POST", body: JSON.stringify({ id: crypto.randomUUID(), name: name.trim(), selectedAgentId: null, createdAt: now, lastOpenedAt: now }) });
      await dashboard.load();
      lock.current = false; selectSession(value.id);
    } catch (cause) { setError(sessionErrorKey(cause)); }
    finally { lock.current = false; setBusy(false); }
  };
  const send = async () => {
    if (lock.current || savedHistory.loading || projectData.projectBusy || fileData.fileBusy || !sessionId || !draft.trim()) return;
    if (!teamId && !defaultTeamId && !/^\/team\s+\S+/i.test(draft)) { setError("Choose a team for this request or save a default team for this session."); return; }
    lock.current = true; setBusy(true); setError("");
    const pending = new AbortController(); controller.current = pending;
    setSending(true);
    const requestId = execution.begin();
    try {
      await request<{ runId: string; output: string }>(`/api/sessions/${sessionId}/messages`, { method: "POST", signal: pending.signal, body: JSON.stringify({ requestId, ...(teamId ? { teamId } : {}), input: draft, allowFileEgress: fileData.allowFileEgress, fileApprovalScope: fileData.fileApprovalScope }) });
      setDraft(""); await savedHistory.refresh();
    } catch (cause) {
      const failure = pending.signal.aborted ? "Request stopped. Refresh history before sending again." : sessionErrorKey(cause);
      setError(failure);
      if (!pending.signal.aborted) {
        await savedHistory.refresh(); // Preserve the original failure and the unsent draft; never retry a send.
      }
    }
    finally { controller.current = null; lock.current = false; setBusy(false); setSending(false); fileData.resetFileConsent(); void execution.finish(requestId); }
  };
  const saveDefaultTeam = async (clear = false) => {
    if (lock.current || !sessionId) return;
    lock.current = true; setBusy(true); setError(""); setTeamSaved(false);
    try {
      const saved = await request<{ teamId: string | null }>(`/api/sessions/${sessionId}/team`, { method: "PUT", body: JSON.stringify({ teamId: clear ? null : teamId || null }) });
      setDefaultTeamId(saved.teamId ?? ""); setTeamSaved(true);
    } catch (cause) { setError(sessionErrorKey(cause)); }
    finally { lock.current = false; setBusy(false); }
  };
  return {
    executionEvents: execution.events,
    ...fileData, ...projectData, sessionId, messages: savedHistory.messages,
    historyError: savedHistory.failed ? "Conversation history could not be loaded. Retry without creating a new session." as TranslationKey : "",
    historyHasMore: savedHistory.hasMore, loadOlder: savedHistory.loadOlder, historyLoadingOlder: savedHistory.loadingOlder,
    teams: teamData.teams, teamsLoading: teamData.loading, teamsError: teamData.error, refreshTeams: teamData.refresh,
    teamId, setTeamId: (value: string) => { fileData.resetFileConsent(); setTeamId(value); },
    setProject: (value: string) => { fileData.resetFileConsent(); return projectData.setProject(value); },
    defaultTeamId, teamSaved, saveDefaultTeam, draft, setDraft, loading: savedHistory.loading,
    busy, sending, error, selectSession, create, send, refresh, cancel: () => controller.current?.abort(),
  };
}
