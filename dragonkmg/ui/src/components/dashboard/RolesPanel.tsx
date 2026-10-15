import { Panel } from "../ui/Panel";
import { List } from "../ui/List";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";

export function RolesPanel({ dashboard }: { dashboard: DashboardController }) {
  const { t } = useI18n();
  const { roles, busy, submit } = dashboard;
  return (
    <Panel
      title="Roles library"
      caption="Role instructions apply before agent and skill instructions."
    >
      <form onSubmit={(e) => void submit(e, "roles")}>
        <label>
          {t("Role name")}
          <input name="name" placeholder={t("Role name")} required />
        </label>
        <label>
          {t("Purpose")}
          <input name="description" placeholder={t("Purpose")} required />
        </label>
        <label>
          {t("Default role instructions")}
          <textarea
            name="instructions"
            placeholder={t("Default role instructions")}
            required
          />
        </label>
        <button disabled={busy}>{t("Create role")}</button>
      </form>
      <List
        values={roles}
        render={(x) => (
          <>
            <strong>{x.name}</strong>
            <span>{x.description}</span>
            <em>v{x.version}</em>
          </>
        )}
      />
    </Panel>
  );
}
