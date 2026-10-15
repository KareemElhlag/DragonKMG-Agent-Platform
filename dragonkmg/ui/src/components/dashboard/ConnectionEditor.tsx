import { useState } from "react";
import type { FormEvent } from "react";
import type { Connection } from "../../models";
import { providerKinds } from "../../providerKinds";
import { useI18n } from "../../i18n";

export function ConnectionEditor({ connection, busy, onSave }: { connection?: Connection; busy: boolean; onSave: (event: FormEvent<HTMLFormElement>) => void }) {
  const { t } = useI18n();
  const [auth, setAuth] = useState(connection ? (connection.secretReference ? "reference" : "none") : "key");
  return <form onSubmit={onSave}>
    <label>{t("Connection name")}<input name="name" defaultValue={connection?.name ?? ""} required /></label>
    <div className="form-columns">
      <label>{t("Connection type")}<select name="trustZone" defaultValue={connection?.trustZone ?? 0}>
        <option value="0">{t("Local")}</option><option value="2">{t("Remote")}</option><option value="1">{t("Cloud")}</option>
      </select></label>
      <label>{t("Provider protocol")}<select name="kind" defaultValue={connection?.kind ?? 0} onChange={event => { if (event.target.value === "1" && !connection) setAuth("none") }}>
        {providerKinds.map(kind => <option key={kind.value} value={kind.value}>{kind.label}</option>)}
      </select></label>
    </div>
    <label>{t("Endpoint URL")}<input name="endpoint" type="url" dir="ltr" placeholder="https://your-provider.example/v1" defaultValue={connection?.endpoint ?? ""} required /><small>{t("Use the API base URL. Existing /v1 and proxy prefixes are preserved.")}</small></label>
    <label>{t("Authentication")}<select name="authMode" value={auth} onChange={event => setAuth(event.target.value)}>
      <option value="key">{t("API key in OS vault")}</option><option value="reference">{t("Secret reference")}</option><option value="none">{t("No authentication")}</option>
    </select></label>
    {auth === "key" && <label>{t("API key")}<input name="apiKey" type="password" autoComplete="off" required dir="ltr" /><small>{t("Stored encrypted on Windows. Use env:NAME on other systems.")}</small></label>}
    {auth === "reference" && <label>{t("Secret reference")}<input name="secretEnv" dir="ltr" defaultValue={connection?.secretReference ?? ""} placeholder="env:PROVIDER_KEY" pattern="(env|vault):[A-Za-z_][A-Za-z0-9_-]*" required /></label>}
    <button disabled={busy}>{connection ? t("Save") : t("Add connection")}</button>
  </form>;
}
