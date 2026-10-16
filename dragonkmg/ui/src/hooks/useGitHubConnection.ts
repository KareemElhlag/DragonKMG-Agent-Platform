import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError, request } from "../services/api";

export type GitHubStatus = { connected: boolean; login: string | null; vaultSupported: boolean };
export type GitHubRepository = { id: number; fullName: string; private: boolean; htmlUrl: string };
type GitHubBinding = { projectId: string; fullName: string; boundAt: string };
export function useGitHubConnection(projectId: string) {
  const [status, setStatus] = useState<GitHubStatus | null>(null);
  const [repositories, setRepositories] = useState<GitHubRepository[]>([]);
  const [binding, setBinding] = useState<GitHubBinding | null>(null);
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<"" | "invalid" | "failed">("");
  const generation = useRef(0);
  const invalidate = useCallback(() => { generation.current++; }, []);
  const lock = useRef(false);
  const refresh = useCallback(async () => {
    const current = ++generation.current;
    setBusy(true); setError(""); setRepositories([]); setBinding(null);
    try {
      const value = await request<GitHubStatus>("/api/integrations/github", { signal: AbortSignal.timeout(30000) });
      if (current === generation.current) setStatus(value);
      const [repos, saved] = await Promise.all([
        value.connected ? request<GitHubRepository[]>(`/api/integrations/github/repositories?page=${page}`, { signal: AbortSignal.timeout(30000) }) : Promise.resolve([]),
        projectId ? request<{ binding: GitHubBinding | null }>(`/api/projects/${encodeURIComponent(projectId)}/github`, { signal: AbortSignal.timeout(30000) }).then(value => value.binding) : Promise.resolve(null),
      ]);
      if (current === generation.current) { setStatus(value); setRepositories(repos); setBinding(saved); }
    } catch { if (current === generation.current) setError("failed"); }
    finally { if (current === generation.current) setBusy(false); }
  }, [page, projectId]);
  useEffect(() => { let active = true; void Promise.resolve().then(() => { if (active) void refresh(); }); return () => { active = false; invalidate(); }; }, [refresh, invalidate]);
  const mutate = async (path: string, method: string, body?: object) => {
    if (lock.current || busy) return;
    const current = generation.current;
    lock.current = true; setBusy(true); setError("");
    try {
      await request(path, { method, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(30000) });
      if (current === generation.current) await refresh();
    } catch (cause) { if (current === generation.current) setError(cause instanceof ApiError && cause.code === "GitHubTokenInvalid" ? "invalid" : "failed"); }
    finally { lock.current = false; if (current === generation.current) setBusy(false); }
  };
  return { status, repositories, binding, page, busy, error, refresh,
    connect: (token: string) => mutate("/api/integrations/github", "POST", { token }),
    disconnect: () => mutate("/api/integrations/github", "DELETE"),
    bind: (fullName: string) => projectId ? mutate(`/api/projects/${encodeURIComponent(projectId)}/github`, "PUT", { fullName }) : Promise.resolve(),
    previous: () => setPage(value => Math.max(1, value - 1)),
    next: () => setPage(value => Math.min(100, value + 1)),
  };
}
