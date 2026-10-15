import { Panel } from "../ui/Panel";
import { List } from "../ui/List";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";

export function AgentTestPanel({
  dashboard,
}: {
  dashboard: DashboardController;
}) {
  const { t, locale } = useI18n();
  const {
    busy,
    agents,
    selectAgent,
    selectedAgentId,
    testInput,
    run,
    runs,
    testAgent,
    setTestInput,
  } = dashboard;
  return (
    <Panel
      title="Test agent"
      caption="Runs the selected agent through the real policy and provider path; no UI-only preview."
    >
      <label>
        {t("Select agent")}
        <select
          value={selectedAgentId}
          disabled={busy}
          onChange={(e) => void selectAgent(e.target.value)}
        >
          <option value="">{t("Select agent")}</option>
          {agents.map((agent) => (
            <option key={agent.id} value={agent.id}>
              {agent.name} · {agent.modelId}
            </option>
          ))}
        </select>
      </label>
      <textarea
        value={testInput}
        aria-label={t("Test task")}
        onChange={(event) => setTestInput(event.target.value)}
        placeholder={t("Test task")}
      />
      <button
        onClick={() => void testAgent()}
        disabled={busy || !selectedAgentId || !testInput.trim()}
      >
        {t("Run test")}
      </button>
      {run && (
        <div className="run-result">
          <strong>
            {[
              t("Queued"),
              t("In progress"),
              t("Succeeded"),
              t("Failed"),
              t("Blocked"),
            ][run.status] ?? t("Unknown")}
          </strong>
          <pre>
            {run.output || run.errorCode || t("No provider output returned.")}
          </pre>
        </div>
      )}
      <div className="run-history">
        <h3>{t("Run history")}</h3>
        <List
          values={runs}
          render={(value) => (
            <>
              <strong>
                {[
                  t("Queued"),
                  t("In progress"),
                  t("Succeeded"),
                  t("Failed"),
                  t("Blocked"),
                ][value.status] ?? t("Unknown")}
              </strong>
              <span>
                {new Date(value.startedAt).toLocaleString(locale)} ·{" "}
                {value.input}
              </span>
              <em>{value.errorCode || value.id.slice(0, 8)}</em>
            </>
          )}
        />
      </div>
    </Panel>
  );
}
