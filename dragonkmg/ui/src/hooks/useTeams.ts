import { useCallback, useEffect, useState } from "react";
import { request, providerErrorKey } from "../services/api";
import type { TeamDefinition } from "../models";
import type { TranslationKey } from "../i18n";

export function useTeams() {
  const [teams, setTeams] = useState<TeamDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<TranslationKey | "">("");
  const refresh = useCallback(async () => {
    setLoading(true); setError("");
    try { setTeams(await request<TeamDefinition[]>("/api/teams")); }
    catch (cause) { setError(providerErrorKey(cause)); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { let active = true; void Promise.resolve().then(() => { if (active) void refresh(); }); return () => { active = false; }; }, [refresh]);
  return { teams, loading, error, refresh };
}
