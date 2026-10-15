import type { DashboardController } from "../../hooks/useDashboard";
import { useI18n } from "../../i18n";
import { Panel } from "../ui/Panel";
import { useState } from "react";
export function AccessSettings({
  dashboard: d,
}: {
  dashboard: DashboardController;
}) {
  const { t } = useI18n();
  const [draft, setDraft] = useState(d.localApiToken);
  return (
    <Panel
      title="Global access key"
      caption="Browser-session token. It is removed when this browser session ends."
    >
      <p>{t("Use the local management token, not your model provider API key.")}</p>
      <form onSubmit={event => { event.preventDefault(); d.updateLocalToken(draft.trim()); void d.load(); }}>
      <label>
        {t("Local API token")}
        <input
          type="password"
          autoComplete="off"
          value={draft}
          required
          onChange={(e) => setDraft(e.target.value)}
        />
      </label>
      <button type="submit" disabled={d.busy || d.loading || !draft.trim()}>
        {t("Save")}
      </button>
      </form>
    </Panel>
  );
}
