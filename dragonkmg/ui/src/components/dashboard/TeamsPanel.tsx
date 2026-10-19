import { useEffect, useMemo, useState } from "react";
import { useI18n } from "../../i18n";
import { useTeams } from "../../hooks/useTeams";
import type { DashboardController } from "../../hooks/useDashboard";
import type { TeamDefinition } from "../../models";
import type { TranslationKey } from "../../i18n";
import { request, providerErrorKey, ApiError } from "../../services/api";
import { Panel } from "../ui/Panel";

export function TeamsPanel({ dashboard }: { dashboard: DashboardController }) {
  const { t } = useI18n();
  const data = useTeams();
  const [draft, setDraft] = useState<TeamDefinition | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<TranslationKey | "">("");
  const [saved, setSaved] = useState(false);
  const [selectedTemplateKey, setSelectedTemplateKey] = useState("");
  const [connectionId, setConnectionId] = useState("");
  const [modelId, setModelId] = useState("");
  const [provisionBusy, setProvisionBusy] = useState(false);
  const [provisionError, setProvisionError] = useState<TranslationKey | "">("");
  const [provisionedTeam, setProvisionedTeam] = useState<TeamDefinition | null>(null);
  const [provisionedAgentCount, setProvisionedAgentCount] = useState(0);
  const selectedTemplate = useMemo(() => data.templates.find(template => template.key === selectedTemplateKey) ?? data.templates[0], [data.templates, selectedTemplateKey]);
  const availableConnections = dashboard.connections.filter(connection => connection.enabled);
  const availableModels = dashboard.connectionModels.filter(model => model.connectionId === connectionId && model.enabled);
  useEffect(() => {
    if (!selectedTemplateKey && data.templates.length) {
      const featured = data.templates.find(template => template.key.toLowerCase().includes("nexus-feature")) ?? data.templates[0];
      setSelectedTemplateKey(featured.key);
    }
  }, [data.templates, selectedTemplateKey]);
  useEffect(() => {
    if (!availableConnections.some(connection => connection.id === connectionId)) {
      setConnectionId(availableConnections.find(connection => connection.id === dashboard.defaultConnectionId)?.id ?? availableConnections[0]?.id ?? "");
    }
  }, [availableConnections, connectionId, dashboard.defaultConnectionId]);
  useEffect(() => {
    if (!availableModels.some(model => model.modelId === modelId)) {
      setModelId(availableModels.find(model => model.isDefault)?.modelId ?? availableModels[0]?.modelId ?? "");
    }
  }, [availableModels, modelId]);
  const save = async () => {
    if (!draft || busy) return;
    setBusy(true); setError(""); setSaved(false);
    try {
      const version = data.teams.some(team => team.id === draft.id) ? draft.version + 1 : 1;
      await request(`/api/teams/${draft.id}`, { method: "PUT", body: JSON.stringify({ ...draft, name: draft.name.trim(), version }) });
      await data.refresh(); setDraft(null); setSaved(true);
    } catch (cause) { setError(cause instanceof ApiError && cause.code === "TeamConflict" ? "Team changed elsewhere. Reload teams before saving again." : providerErrorKey(cause)); }
    finally { setBusy(false); }
  };
  const createFromTemplate = async () => {
    if (!selectedTemplate || !connectionId || !modelId || provisionBusy) return;
    setProvisionBusy(true); setProvisionError(""); setProvisionedTeam(null);
    try {
      const result = await data.provision(selectedTemplate.key, connectionId, modelId);
      setProvisionedTeam(result.team);
      setProvisionedAgentCount(result.agents.length);
      await Promise.all([data.refresh(), dashboard.load()]);
    } catch (cause) {
      setProvisionError(providerErrorKey(cause) || "Provisioning failed. Review the connection and model, then retry.");
    } finally { setProvisionBusy(false); }
  };
  return <Panel title="Teams" caption="Named teams run their enabled agents in order. Skills and connections remain assigned to each agent.">
    <section className="team-provision" aria-labelledby="team-provision-title">
      <header className="team-provision-heading">
        <div><p className="eyebrow">{t("Ready-made team")}</p><h2 id="team-provision-title"><bdi>{selectedTemplate?.name ?? t("Nexus Feature Full Production")}</bdi></h2><p>{t("Build NexusCore features with a staged, specialist review pipeline.")}</p></div>
        <span className="badge">{selectedTemplate?.agents.length ?? 0} {t("agents in sequence")}</span>
      </header>
      {data.templatesLoading ? <p role="status">{t("Loading team templates…")}</p> : data.templatesError ? <p role="alert" className="error">{t("Could not load team templates.")} <button type="button" onClick={() => void data.refreshTemplates()}>{t("Retry")}</button></p> : !selectedTemplate ? <p>{t("No team templates are available.")}</p> : <>
        {data.templates.length > 1 && <label>{t("Team template")}<select value={selectedTemplate.key} onChange={event => { setSelectedTemplateKey(event.target.value); setProvisionedTeam(null); }}>
          {data.templates.map(template => <option value={template.key} key={template.key}>{template.name}</option>)}
        </select></label>}
        <p className="team-template-description">{selectedTemplate.description}</p>
        <h3>{t("Execution order")}</h3>
        <ol className="team-pipeline">{selectedTemplate.agents.map((agent, index) => <li key={`${agent.roleId}-${index}`}>
          <span className="team-pipeline-index" aria-hidden="true">{index + 1}</span>
          <div><strong><bdi>{agent.name}</bdi></strong><p>{agent.description}</p>{agent.skillKeys.length > 0 && <div className="team-skill-keys" aria-label={t("Skills")}>{agent.skillKeys.map(key => <span className="badge" key={key}><bdi>{key}</bdi></span>)}</div>}</div>
        </li>)}</ol>
        <p className="muted">{t("Choose connection and model for this team; these will be applied to its agents.")}</p>
        <div className="team-provision-controls">
          <label>{t("Model connection")}<select required value={connectionId} disabled={provisionBusy || availableConnections.length === 0} onChange={event => { setConnectionId(event.target.value); setModelId(""); setProvisionError(""); }}>
            <option value="">{t("Choose a connection")}</option>{availableConnections.map(connection => <option key={connection.id} value={connection.id}>{connection.name}</option>)}
          </select></label>
          <label>{t("Select model")}<select required value={modelId} disabled={provisionBusy || !connectionId || availableModels.length === 0} onChange={event => { setModelId(event.target.value); setProvisionError(""); }}>
            <option value="">{t("Choose a model")}</option>{availableModels.map(model => <option key={model.modelId} value={model.modelId}>{model.displayName}{model.isDefault ? ` · ${t("Default model")}` : ""}</option>)}
          </select></label>
        </div>
        {!availableConnections.length && <p className="muted">{t("Add and enable a model connection before provisioning this team.")}</p>}
        {!!connectionId && !availableModels.length && <p className="muted">{t("No enabled models for this connection.")}</p>}
        {provisionError && <p role="alert" className="error">{t(provisionError)} <button type="button" onClick={() => void createFromTemplate()} disabled={provisionBusy || !connectionId || !modelId}>{t("Retry")}</button></p>}
        {provisionedTeam && <p role="status" className="team-provision-success">{t("Team provisioned")} — <bdi>{provisionedTeam.name}</bdi> · {provisionedAgentCount} {t("agents created")}</p>}
        <button type="button" onClick={() => void createFromTemplate()} disabled={provisionBusy || !connectionId || !modelId || !selectedTemplate.agents.length}>
          {provisionBusy ? t("Provisioning…") : t("Provision team")}
        </button>
      </>}
    </section>
    <button type="button" disabled={busy} onClick={() => { setSaved(false); setDraft({ id: crypto.randomUUID(), name: "", description: "", enabled: true, agentIds: [], version: 1 }); }}>{t("New team")}</button>
    {(error || data.error) && <p role="alert" className="error">{t(error || data.error as TranslationKey)} <button type="button" onClick={() => void data.refresh()}>{t("Retry")}</button></p>}
    {saved && <p role="status">{t("Team saved")}</p>}
    {data.loading ? <p role="status">{t("Loading history…")}</p> : data.teams.length === 0 ? <p>{t("No teams yet. Create a planning, review, workflow, or custom team.")}</p> : <div className="team-grid">{data.teams.map(team => <article className="team-member" key={team.id}><strong>{team.name}</strong><span className="badge">{t(team.enabled ? "Enabled" : "Disabled")}</span><p>{team.description}</p><ol>{team.agentIds.map(id => <li key={id}>{dashboard.agents.find(agent => agent.id === id)?.name ?? id}</li>)}</ol><button type="button" disabled={busy} onClick={() => { setSaved(false); setDraft({ ...team, agentIds: [...team.agentIds] }); }}>{t("Edit")}</button></article>)}</div>}
    {draft && <form onSubmit={event => { event.preventDefault(); void save(); }}><fieldset disabled={busy}>
      <label>{t("Team name")}<input required maxLength={100} value={draft.name} onChange={event => setDraft({ ...draft, name: event.target.value })} /></label>
      <label>{t("Description")}<textarea maxLength={2000} value={draft.description} onChange={event => setDraft({ ...draft, description: event.target.value })} /></label>
      <label><input type="checkbox" checked={draft.enabled} onChange={event => setDraft({ ...draft, enabled: event.target.checked })} />{t("Enabled")}</label>
      <h3>{t("Execution order")}</h3><ol>{draft.agentIds.map((id, index) => <li key={id}><bdi>{dashboard.agents.find(agent => agent.id === id)?.name ?? id}</bdi> <button type="button" disabled={index === 0} aria-label={`${t("Move earlier")} ${index + 1}`} onClick={() => { const ids = [...draft.agentIds]; [ids[index - 1], ids[index]] = [ids[index], ids[index - 1]]; setDraft({ ...draft, agentIds: ids }); }}>↑</button> <button type="button" onClick={() => setDraft({ ...draft, agentIds: draft.agentIds.filter(value => value !== id) })}>{t("Remove")}</button></li>)}</ol>
      <label>{t("Add agent")}<select value="" disabled={draft.agentIds.length >= 16} onChange={event => { if (event.target.value) setDraft({ ...draft, agentIds: [...draft.agentIds, event.target.value] }); }}><option value="">{t("Select agent")}</option>{dashboard.agents.filter(agent => agent.enabled && !draft.agentIds.includes(agent.id)).map(agent => <option key={agent.id} value={agent.id}>{agent.name}</option>)}</select></label>
      <button type="submit" disabled={!draft.name.trim() || draft.agentIds.length === 0}>{t("Save")}</button> <button type="button" onClick={() => setDraft(null)}>{t("Cancel")}</button>
    </fieldset></form>}
  </Panel>;
}
