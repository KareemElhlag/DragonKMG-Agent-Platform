import { ModelPicker } from "./ModelPicker";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";

export function CouncilSetup({
  dashboard: d,
}: {
  dashboard: DashboardController;
}) {
  const { t } = useI18n();
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        const form = new FormData(event.currentTarget);
        void d.provisionCouncil({ connectionId: d.councilConnectionId, modelId: String(form.get("modelId") ?? "") });
      }}
    >
      <label>
        {t("Select model connection")}
        <select
          required
          value={d.councilConnectionId}
          onChange={(event) => d.setCouncilConnectionId(event.target.value)}
        >
          <option value="">{t("Select model connection")}</option>
          {d.connections
            .filter((connection) => connection.enabled)
            .map((connection) => (
              <option value={connection.id} key={connection.id}>
                {connection.name}
              </option>
            ))}
        </select>
      </label>
      <ModelPicker key={d.councilConnectionId + ":" + (d.connectionModels.find(model => model.connectionId === d.councilConnectionId && model.isDefault)?.modelId ?? "")} models={d.connectionModels} connectionId={d.councilConnectionId} />
      {!d.connections.some((connection) => connection.enabled) && (
        <p>
          {t(
            "Configure a model connection first. The team will keep independent agent bindings.",
          )}
        </p>
      )}
      <button
        disabled={d.busy || !d.councilConnectionId}
      >
        {t("Create default council")}
      </button>
    </form>
  );
}
