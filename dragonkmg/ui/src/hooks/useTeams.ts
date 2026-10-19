import { useCallback, useEffect, useState } from "react";
import { request, providerErrorKey } from "../services/api";
import type { TeamDefinition, TeamTemplate } from "../models";
import type { TranslationKey } from "../i18n";

export function useTeams() {
  const [teams, setTeams] = useState<TeamDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<TranslationKey | "">("");
  const [templates, setTemplates] = useState<TeamTemplate[]>([]);
  const [templatesLoading, setTemplatesLoading] = useState(true);
  const [templatesError, setTemplatesError] = useState<TranslationKey | "">("");
  const refresh = useCallback(async () => {
    setLoading(true); setError("");
    try { setTeams(await request<TeamDefinition[]>("/api/teams")); }
    catch (cause) { setError(providerErrorKey(cause)); }
    finally { setLoading(false); }
  }, []);
  const refreshTemplates = useCallback(async () => {
    setTemplatesLoading(true);
    setTemplatesError("");
    try {
      const response = await request<TeamTemplate[] | TeamTemplate>("/api/teams/templates");
      setTemplates(Array.isArray(response) ? response : [response]);
    } catch (cause) {
      setTemplatesError(providerErrorKey(cause));
    } finally {
      setTemplatesLoading(false);
    }
  }, []);
  const provision = useCallback(async (templateKey: string, connectionId: string, modelId: string) => {
    return request<{ team: TeamDefinition; agents: unknown[] }>("/api/teams/provision", {
      method: "POST",
      body: JSON.stringify({ templateKey, connectionId, modelId }),
    });
  }, []);
  useEffect(() => { let active = true; void Promise.resolve().then(() => { if (active) void refresh(); }); return () => { active = false; }; }, [refresh]);
  useEffect(() => { let active = true; void Promise.resolve().then(() => { if (active) void refreshTemplates(); }); return () => { active = false; }; }, [refreshTemplates]);
  return { teams, loading, error, refresh, templates, templatesLoading, templatesError, refreshTemplates, provision };
}
