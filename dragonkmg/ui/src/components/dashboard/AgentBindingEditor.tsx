import { useState } from "react";
import type { Agent } from "../../models";
import type { DashboardController } from "../../hooks/useDashboard";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n";
import { ModelPicker } from "./ModelPicker";

export function AgentBindingEditor({ agent, dashboard: d }: { agent: Agent; dashboard: DashboardController }) {
  const { t } = useI18n();
  const [connectionId, setConnectionId] = useState(agent.connectionId);
  const initial = connectionId === agent.connectionId ? agent.modelId : "";
  const modelKey = connectionId + ":" + (d.connectionModels.find(model => model.connectionId === connectionId && model.isDefault)?.modelId ?? "");
  return <form className="binding-form" onSubmit={event => void d.updateAgentBinding(event, agent)}>
    <strong>{agent.name}</strong>
    <label>{t("Model connections")}<select name="connectionId" value={connectionId} onChange={event => setConnectionId(event.target.value)} required>
      {d.connections.map(connection => <option value={connection.id} key={connection.id}>{connection.name}</option>)}
    </select></label>
    <ModelPicker key={modelKey} models={d.connectionModels} connectionId={connectionId} initialValue={initial} />
    <label>{t("Mode")}<select name="mode" defaultValue={agent.mode}>
      {["Execution", "Plan only", "Question only", "Review"].map((mode, index) => <option value={index} key={mode}>{t(mode as TranslationKey)}</option>)}
    </select></label>
    <button disabled={d.busy}>{t("Save")}</button>
  </form>;
}
