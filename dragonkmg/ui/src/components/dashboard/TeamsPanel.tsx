import { useState } from "react";
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
  return <Panel title="Teams" caption="Named teams run their enabled agents in order. Skills and connections remain assigned to each agent.">
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
