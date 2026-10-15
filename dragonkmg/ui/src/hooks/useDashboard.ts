import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import type {
  Agent,
  Role,
  Skill,
  Connection,
  ConnectionModel,
  Assignment,
  ToolGrant,
  AgentRun,
  CouncilRun,
  CouncilWorkflowStep,
  TerminalSession,
  SessionMessage,
} from "../models";
import { request, providerErrorKey } from "../services/api";
import type { TranslationKey } from "../i18n";
const id = () => crypto.randomUUID();
export function useDashboard() {
  const [authRequired, setAuthRequired] = useState(() => !sessionStorage.getItem("dragonkmg.localApiToken"));
  useEffect(() => {
    const requireAuthentication = () => setAuthRequired(true);
    window.addEventListener("dragonkmg:authentication-required", requireAuthentication);
    return () => window.removeEventListener("dragonkmg:authentication-required", requireAuthentication);
  }, []);
  const agentGeneration = useRef(0);
  const sessionGeneration = useRef(0);
  const [agentLoading, setAgentLoading] = useState(false);
  const [roles, setRoles] = useState<Role[]>([]);
  const [skills, setSkills] = useState<Skill[]>([]);
  const [connectionModels, setConnectionModels] = useState<ConnectionModel[]>([]);
  const [connections, setConnections] = useState<Connection[]>([]);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [defaultConnectionId, setDefaultConnectionId] = useState("");
  const [loading, setLoading] = useState(false);
  const [notice, setNotice] = useState<TranslationKey>("Loading workspace…");
  const [error, setError] = useState<TranslationKey | "">("");
  const [busy, setBusy] = useState(false);
  const [selectedAgentId, setSelectedAgentId] = useState("");
  const [selectedSkillId, setSelectedSkillId] = useState("");
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [toolGrants, setToolGrants] = useState<ToolGrant[]>([]);
  const [toolIds, setToolIds] = useState("");
  const [testInput, setTestInput] = useState("");
  const [run, setRun] = useState<AgentRun | null>(null);
  const [runs, setRuns] = useState<AgentRun[]>([]);
  const [localApiToken, setLocalApiToken] = useState(
    () => sessionStorage.getItem("dragonkmg.localApiToken") ?? "",
  );
  const [councilConnectionId, setCouncilConnectionId] = useState("");
  const [councilModelId, setCouncilModelId] = useState("");
  const [councilInput, setCouncilInput] = useState("");
  const [councilRun, setCouncilRun] = useState<CouncilRun | null>(null);
  const [teamTemplates, setTeamTemplates] = useState<
    {
      name: string;
      description: string;
      roleId: string;
      instructions: string;
    }[]
  >([]);
  const [workflow, setWorkflow] = useState<CouncilWorkflowStep[]>([]);
  const [sessions, setSessions] = useState<TerminalSession[]>([]);
  const [sessionMessages, setSessionMessages] = useState<SessionMessage[]>([]);
  const [selectedSessionId, setSelectedSessionId] = useState("");
  const load = useCallback(async () => {
    setError("");
    setLoading(true);
    try {
      const [r, s, c, a, w, t, defaultBinding, templates, models] = await Promise.all([
        request<Role[]>("/api/roles"),
        request<Skill[]>("/api/skills"),
        request<Connection[]>("/api/connections"),
        request<Agent[]>("/api/agents"),
        request<CouncilWorkflowStep[]>("/api/council/default/workflow"),
        request<TerminalSession[]>("/api/sessions"),
        request<{ connectionId: string | null }>(
          "/api/settings/default-connection",
        ),
        request<
          {
            name: string;
            description: string;
            roleId: string;
            instructions: string;
          }[]
        >("/api/council/default/templates"),
        request<ConnectionModel[]>("/api/models"),
      ]);
      setConnectionModels(models);
      setAuthRequired(false);
      setTeamTemplates(templates);
      setRoles(r);
      setSkills(s);
      setConnections(c);
      setAgents(a);
      setWorkflow(w);
      setSessions(t);
      setDefaultConnectionId(defaultBinding.connectionId ?? "");
      setCouncilConnectionId(
        (current) => current || defaultBinding.connectionId || "",
      );
      setNotice("Workspace synchronized.");
      return true;
    } catch (cause) {
      setError(providerErrorKey(cause));
      return false;
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    void Promise.resolve().then(load);
  }, [load]);
  const selectAgent = async (agentId: string) => {
    if (busy || agentLoading) return;
    const generation = ++agentGeneration.current;
    setSelectedAgentId(agentId);
    setAssignments([]);
    setToolGrants([]);
    setToolIds("");
    setRuns([]);
    setRun(null);
    if (!agentId) return;
    setAgentLoading(true);
    try {
      const [nextAssignments, nextRuns, nextGrants] = await Promise.all([
        request<Assignment[]>(`/api/agents/${agentId}/skills`),
        request<AgentRun[]>(`/api/agents/${agentId}/runs`),
        request<ToolGrant[]>(`/api/agents/${agentId}/tools`),
      ]);
      if (generation !== agentGeneration.current) return;
      setAssignments(nextAssignments);
      setRuns(nextRuns);
      setToolGrants(nextGrants);
      setToolIds(nextGrants.map((grant) => grant.toolId).join(", "));
    } catch (cause) {
      if (generation !== agentGeneration.current) return;
      setSelectedAgentId("");
      setError(providerErrorKey(cause));
    } finally {
      if (generation === agentGeneration.current) setAgentLoading(false);
    }
  };
  const submit = async (
    event: FormEvent<HTMLFormElement>,
    resource: "roles" | "skills" | "connections" | "agents",
  ) => {
    event.preventDefault();
    setBusy(true);
    setError("");
    const formElement = event.currentTarget;
    const form = new FormData(formElement);
    try {
      if (resource === "roles")
        await request("/api/roles", {
          method: "POST",
          body: JSON.stringify({
            id: id(),
            name: form.get("name"),
            description: form.get("description"),
            defaultInstructions: form.get("instructions"),
            isSystem: false,
            version: 1,
          }),
        });
      if (resource === "skills") {
        const existing = skills.find((skill) => skill.id === form.get("id"));
        const sourceType =
          form.get("sourceType") === "project-file" ? "project-file" : "inline";
        await request("/api/skills", {
          method: "POST",
          body: JSON.stringify({
            id: existing?.id ?? id(),
            key: form.get("key"),
            name: form.get("name"),
            description: form.get("description"),
            version: existing ? existing.version + 1 : 1,
            enabled: existing?.enabled ?? true,
            type: existing?.type ?? 0,
            instructionContent:
              sourceType === "inline" ? form.get("instructions") : "",
            sourceType,
            sourceRef:
              sourceType === "project-file" ? form.get("sourceRef") : "",
            requiredToolIdsJson: existing?.requiredToolIdsJson ?? "[]",
            trustLevel: existing?.trustLevel ?? "local",
          }),
        });
      }
      if (resource === "connections") {
        const auth = form.get("authMode");
        await request("/api/connections/configure", {
          method: "POST",
          body: JSON.stringify({
            connection: {
              id: id(), name: form.get("name"), kind: Number(form.get("kind")), endpoint: form.get("endpoint"),
              secretReference: auth === "reference" ? String(form.get("secretEnv") ?? "") : "",
              enabled: true, trustZone: Number(form.get("trustZone")), cloudEgressAllowed: false, createdAt: new Date().toISOString()
            },
            apiKey: auth === "key" ? form.get("apiKey") : null
          }),
        });
      }
      if (resource === "agents") {
        const now = new Date().toISOString();
        await request("/api/agents", {
          method: "POST",
          body: JSON.stringify({
            id: id(),
            name: form.get("name"),
            description: form.get("description"),
            roleId: form.get("roleId"),
            instructions: form.get("instructions"),
            enabled: true,
            modelId: form.get("modelId"),
            connectionId: form.get("connectionId") || defaultConnectionId,
            version: 1,
            createdAt: now,
            updatedAt: now,
            mode: Number(form.get("mode")),
          }),
        });
      }
      if (!(await load())) return false;
      formElement.reset();
      setNotice("Saved successfully.");
      return true;
    } catch (cause) {
      setError(providerErrorKey(cause));
      return false;
    } finally {
      setBusy(false);
    }
  };
  const assignSkill = async () => {
    if (!selectedAgentId || !selectedSkillId) return;
    setBusy(true);
    setError("");
    try {
      const next = [
        ...assignments.filter((x) => x.skillId !== selectedSkillId),
        {
          agentId: selectedAgentId,
          skillId: selectedSkillId,
          enabled: true,
          priority: assignments.length + 1,
          configurationJson: "{}",
          version: 1,
        },
      ];
      await request(`/api/agents/${selectedAgentId}/skills`, {
        method: "PUT",
        body: JSON.stringify(next),
      });
      setAssignments(next);
      setNotice("Saved successfully.");
    } catch (cause) {
      setError(providerErrorKey(cause));
    } finally {
      setBusy(false);
    }
  };
  const removeSkill = async (skillId: string) => {
    const next = assignments.filter((x) => x.skillId !== skillId);
    setBusy(true);
    setError("");
    try {
      await request(`/api/agents/${selectedAgentId}/skills`, {
        method: "PUT",
        body: JSON.stringify(next),
      });
      setAssignments(next);
      setNotice("Saved successfully.");
    } catch (cause) {
      setError(providerErrorKey(cause));
    } finally {
      setBusy(false);
    }
  };
  const saveToolGrants = async () => {
    if (!selectedAgentId) return;
    setBusy(true);
    setError("");
    try {
      const values = [
        ...new Set(
          toolIds
            .split(",")
            .map((value) => value.trim())
            .filter(Boolean)
            .map((value) => value.toLowerCase()),
        ),
      ].map((toolId) => ({
        agentId: selectedAgentId,
        toolId,
        requiresApproval: true,
        version: 1,
      }));
      await request(`/api/agents/${selectedAgentId}/tools`, {
        method: "PUT",
        body: JSON.stringify(values),
      });
      setToolGrants(values);
      setToolIds(values.map((value) => value.toolId).join(", "));
      setNotice("Saved successfully.");
    } catch (cause) {
      setError(providerErrorKey(cause));
    } finally {
      setBusy(false);
    }
  };
  const testAgent = async () => {
    if (!selectedAgentId || !testInput.trim()) return;
    setBusy(true);
    setError("");
    setRun(null);
    try {
      const result = await request<AgentRun>(
        `/api/agents/${selectedAgentId}/test`,
        { method: "POST", body: JSON.stringify({ input: testInput }) },
      );
      setRun(result);
      setRuns((current) => [
        result,
        ...current.filter((value) => value.id !== result.id),
      ]);
      setNotice("Saved successfully.");
    } catch (cause) {
      setError(providerErrorKey(cause));
    } finally {
      setBusy(false);
    }
  };
  const provisionCouncil = async (selected?: { connectionId: string; modelId: string }) => {
    const selectedConnection = selected?.connectionId ?? councilConnectionId;
    const selectedModel = selected?.modelId ?? councilModelId;
    if (!selectedConnection || !selectedModel.trim()) return;
    setBusy(true);
    setError("");
    try {
      await request("/api/council/default", {
        method: "POST",
        body: JSON.stringify({
          connectionId: selectedConnection,
          modelId: selectedModel,
        }),
      });
      await load();
      setNotice("Saved successfully.");
    } catch (cause) {
      setError(providerErrorKey(cause));
    } finally {
      setBusy(false);
    }
  };
  const runCouncil = async () => {
    if (!councilInput.trim()) return;
    setBusy(true);
    setError("");
    setCouncilRun(null);
    setError("");
    try {
      const result = await request<CouncilRun>("/api/council/default/run", {
        method: "POST",
        body: JSON.stringify({ input: councilInput }),
      });
      setCouncilRun(result);
      setNotice("Saved successfully.");
    } catch (cause) {
      setError(providerErrorKey(cause));
    } finally {
      setBusy(false);
    }
  };
  const toggleCloudEgress = async (connection: Connection) => {
    setBusy(true);
    setError("");
    try {
      await request(`/api/connections/${connection.id}`, {
        method: "PUT",
        body: JSON.stringify({
          ...connection,
          cloudEgressAllowed: !connection.cloudEgressAllowed,
        }),
      });
      await load();
      setNotice("Saved successfully.");
    } catch (cause) {
      setError(providerErrorKey(cause));
    } finally {
      setBusy(false);
    }
  };
  const updateLocalToken = (value: string) => {
    setLocalApiToken(value);
    if (value) sessionStorage.setItem("dragonkmg.localApiToken", value);
    else sessionStorage.removeItem("dragonkmg.localApiToken");
  };
  const selectSession = async (sessionId: string) => {
    const generation = ++sessionGeneration.current;
    setSelectedSessionId(sessionId);
    setSessionMessages([]);
    if (!sessionId) return;
    try {
      const messages = await request<SessionMessage[]>(
        `/api/sessions/${sessionId}/messages`,
      );
      if (generation === sessionGeneration.current)
        setSessionMessages(messages);
    } catch (cause) {
      setError(providerErrorKey(cause));
    }
  };

  const saveDefaultConnection = async (connectionId: string) => {
    setBusy(true);
    setError("");
    try {
      await request("/api/settings/default-connection", {
        method: "PUT",
        body: JSON.stringify({ connectionId: connectionId || null }),
      });
      await load();
      setNotice("Saved successfully.");
    } catch {
      setError("The operation could not be completed.");
    } finally {
      setBusy(false);
    }
  };
  const updateAgentBinding = async (
    event: FormEvent<HTMLFormElement>,
    agent: Agent,
  ) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setBusy(true);
    setError("");
    try {
      await request("/api/agents", {
        method: "POST",
        body: JSON.stringify({
          ...agent,
          connectionId: form.get("connectionId"),
          modelId: form.get("modelId"),
          mode: Number(form.get("mode")),
          version: agent.version + 1,
          updatedAt: new Date().toISOString(),
        }),
      });
      await load();
      setNotice("Saved successfully.");
    } catch {
      setError("The operation could not be completed.");
    } finally {
      setBusy(false);
    }
  };

  return {
    authRequired,
    connectionModels,
    teamTemplates,
    roles,
    skills,
    connections,
    agents,
    notice,
    error,
    busy: busy || agentLoading,
    selectedAgentId,
    selectedSkillId,
    assignments,
    toolGrants,
    toolIds,
    testInput,
    run,
    runs,
    localApiToken,
    councilConnectionId,
    councilModelId,
    councilInput,
    councilRun,
    workflow,
    sessions,
    sessionMessages,
    selectedSessionId,
    load,
    selectAgent,
    submit,
    assignSkill,
    removeSkill,
    saveToolGrants,
    testAgent,
    provisionCouncil,
    runCouncil,
    toggleCloudEgress,
    updateLocalToken,
    selectSession,
    setSelectedSkillId,
    setToolIds,
    setTestInput,
    setCouncilConnectionId,
    setCouncilModelId,
    setCouncilInput,
    defaultConnectionId,
    loading,
    saveDefaultConnection,
    updateAgentBinding,
  };
}
export type DashboardController = ReturnType<typeof useDashboard>;
