import { useState } from "react";
import { ModelPicker } from "./ModelPicker";
import { AgentBindingEditor } from "./AgentBindingEditor";
import { Panel } from "../ui/Panel";
import { List } from "../ui/List";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";

export function AgentsPanel({ dashboard }: { dashboard: DashboardController }) {
  const { t } = useI18n();
  const {
    roles,
    connections,
    agents,
    busy,
    submit,
    defaultConnectionId,
    connectionModels,
  } = dashboard;
  const [selectedConnection, setSelectedConnection] = useState("");
  const effectiveConnection = selectedConnection || defaultConnectionId;
  const modelKey = effectiveConnection + ":" + (connectionModels.find(model => model.connectionId === effectiveConnection && model.isDefault)?.modelId ?? "");
  return (
    <Panel
      title="Agents"
      caption="Choose a behavioral mode: execution, plan without code, answer without code, or review only."
    >
      <form onSubmit={(e) => void submit(e, "agents")}>
        <label>
          {t("Agent name")}
          <input name="name" placeholder={t("Agent name")} required />
        </label>
        <label>
          {t("Purpose")}
          <input name="description" placeholder={t("Purpose")} required />
        </label>
        <select name="mode" aria-label={t("Mode")} defaultValue="0">
          <option value="0">{t("Execution")}</option>
          <option value="1">{t("Plan only — no code")}</option>
          <option value="2">{t("Question only — no code")}</option>
          <option value="3">{t("Review only")}</option>
        </select>
        <select
          name="roleId"
          aria-label={t("Select role")}
          required
          defaultValue=""
        >
          <option value="" disabled>
            {t("Select role")}
          </option>
          {roles.map((x) => (
            <option key={x.id} value={x.id}>
              {x.name}
            </option>
          ))}
        </select>
        <select
          name="connectionId"
          aria-label={t("Select model connection")}
          required={!defaultConnectionId}
          value={selectedConnection}
          onChange={event => setSelectedConnection(event.target.value)}
        >
          <option value="">
            {defaultConnectionId
              ? t("Use default connection")
              : t("Select model connection")}
          </option>
          {connections.map((x) => (
            <option key={x.id} value={x.id}>
              {x.name}
            </option>
          ))}
        </select>
        <ModelPicker key={modelKey} models={connectionModels} connectionId={effectiveConnection} />
        <label>
          {t("Agent instructions")}
          <textarea name="instructions" placeholder={t("Agent instructions")} />
        </label>
        <button disabled={busy || !roles.length || !connections.length}>
          {t("Create agent")}
        </button>
      </form>
      <List
        values={agents}
        render={(x) => (
          <>
            <strong>{x.name}</strong>
            <span>{x.modelId}</span>
            <em>
              {[
                t("Execution"),
                t("Plan only"),
                t("Question only"),
                t("Review"),
              ][x.mode] ?? t("Unknown")}{" "}
              · v{x.version}
            </em>
          </>
        )}
      />
      <h3>{t("Edit agent binding")}</h3>
      {agents.map(agent => <AgentBindingEditor key={agent.id + agent.version} agent={agent} dashboard={dashboard} />)}
    </Panel>
  );
}
