import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, providerErrorKey, request } from "../services/api";
import type { TranslationKey } from "../i18n";

export type WorkspaceProject = { id: string; name: string; rootPath: string; createdAt: string; updatedAt: string };

export function useSessionProjects(sessionId: string) {
  const [projects, setProjects] = useState<WorkspaceProject[]>([]);
  const [binding, setBinding] = useState({ sessionId: "", projectId: "" });
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<TranslationKey | "">("");
  const generation = useRef(0);
  const lock = useRef(false);
  const refresh = useCallback(async () => {
    const current = ++generation.current;
    setLoading(true); setError("");
    try {
      const values = await request<WorkspaceProject[]>("/api/projects");
      const saved = sessionId ? await request<{ projectId: string | null }>(`/api/sessions/${sessionId}/project`) : { projectId: null };
      if (generation.current === current) { setProjects(values); setBinding({ sessionId, projectId: saved.projectId ?? "" }); }
    } catch { if (generation.current === current) setError("Project settings could not be loaded. Conversation history is unaffected."); }
    finally { if (generation.current === current) setLoading(false); }
  }, [sessionId]);
  const invalidate = useCallback(() => { generation.current++; }, []);
  useEffect(() => { let active = true; void Promise.resolve().then(() => { if (active) void refresh(); }); return () => { active = false; invalidate(); }; }, [refresh, invalidate]);
  const setProject = async (projectId: string) => {
    if (lock.current || !sessionId) return;
    const current = generation.current;
    lock.current = true; setBusy(true); setError("");
    try {
      await request(`/api/sessions/${sessionId}/project`, { method: "PUT", body: JSON.stringify({ projectId: projectId || null }) });
      const saved = await request<{ projectId: string | null }>(`/api/sessions/${sessionId}/project`);
      if (current === generation.current) setBinding({ sessionId, projectId: saved.projectId ?? "" });
    } catch (cause) { if (current === generation.current) setError(providerErrorKey(cause)); }
    finally { lock.current = false; setBusy(false); }
  };
  const createProject = async (name: string, rootPath: string) => {
    if (lock.current || !sessionId || !name.trim() || !rootPath.trim()) return;
    const current = generation.current;
    lock.current = true; setBusy(true); setError("");
    try {
      const project = await request<WorkspaceProject>("/api/projects", { method: "POST", body: JSON.stringify({ name: name.trim(), rootPath: rootPath.trim() }) });
      if (current !== generation.current) return;
      setProjects(values => [...values, project].sort((a, b) => a.name.localeCompare(b.name)));
      await request(`/api/sessions/${sessionId}/project`, { method: "PUT", body: JSON.stringify({ projectId: project.id }) });
      const saved = await request<{ projectId: string | null }>(`/api/sessions/${sessionId}/project`);
      if (current !== generation.current) return;
      setBinding({ sessionId, projectId: saved.projectId ?? "" });
      return project;
    } catch (cause) {
      if (current === generation.current) setError(cause instanceof ApiError && cause.code === "ProjectFolderUnavailable" ? "ProjectFolderUnavailable" : providerErrorKey(cause));
    }
    finally { lock.current = false; setBusy(false); }
  };
  return { projects, projectId: binding.sessionId === sessionId ? binding.projectId : "", projectBusy: busy, projectsLoading: loading, projectError: error, setProject, createProject, refreshProjects: refresh };
}
