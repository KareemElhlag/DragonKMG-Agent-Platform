import { Panel } from "../ui/Panel";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";
import { ConnectionEditor } from "./ConnectionEditor";
import { ConnectionCard } from "./ConnectionCard";

export function ConnectionsPanel({ dashboard: d }: { dashboard: DashboardController }) {
  const { t } = useI18n();
  return <Panel title="Model connections" caption="Secrets remain environment references; cloud egress starts disabled.">
    <label>{t("Default connection")}<select disabled={d.busy} value={d.defaultConnectionId} onChange={event => void d.saveDefaultConnection(event.target.value)}>
      <option value="">{t("Select model connection")}</option>
      {d.connections.filter(connection => connection.enabled).map(connection => <option value={connection.id} key={connection.id}>{connection.name}</option>)}
    </select></label>
    <p>{t("New agents inherit the selected connection once; existing agents keep their own bindings.")}</p>
    <details className="new-connection" open={d.connections.length === 0 ? true : undefined}><summary>{t("Add connection")}</summary>
      <ConnectionEditor busy={d.busy} onSave={event => void d.submit(event, "connections")} />
    </details>
    <div className="connections-list">{d.connections.map(connection => <ConnectionCard key={connection.id} connection={connection} dashboard={d} />)}</div>
  </Panel>;
}
