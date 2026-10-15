import { useState } from "react";
import type { FormEvent } from "react";
import type { Connection, ConnectionModel } from "../../models";
import type { DashboardController } from "../../hooks/useDashboard";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n";
import { request, providerErrorKey } from "../../services/api";
import { ConnectionEditor } from "./ConnectionEditor";
import { providerKinds } from "../../providerKinds";

export function ConnectionCard({ connection: c, dashboard: d }: { connection: Connection; dashboard: DashboardController }) {
  const { t } = useI18n();
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<TranslationKey | "">("");
  const [notice, setNotice] = useState<TranslationKey | "">("");
  const [testOutput, setTestOutput] = useState("");
  const models = d.connectionModels.filter(model => model.connectionId === c.id);
  const operate = async (operation: () => Promise<unknown>, refresh = false) => {
    setWorking(true); setError(""); setNotice(""); setTestOutput("");
    try {
      await operation();
      if (refresh && !(await d.load())) return false;
      setNotice("Saved successfully.");
      return true;
    } catch (cause) { setError(providerErrorKey(cause)); return false; }
    finally { setWorking(false) }
  };
  const saveModel = (model: ConnectionModel) => operate(() => request(`/api/connections/${c.id}/models`, { method: "PUT", body: JSON.stringify(model) }), true);
  const addModel = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const element = event.currentTarget; const form = new FormData(element);
    const modelId = String(form.get("modelId") ?? "").trim();
    if (await saveModel({ connectionId: c.id, modelId, displayName: String(form.get("displayName") || modelId).trim(), enabled: true, isDefault: form.get("isDefault") === "on", source: "manual", updatedAt: new Date().toISOString() })) element.reset();
  };
  const saveConnection = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const element = event.currentTarget; const form = new FormData(element);
    const auth = form.get("authMode");
    const connection = { ...c, name: form.get("name"), endpoint: form.get("endpoint"), kind: Number(form.get("kind")), trustZone: Number(form.get("trustZone")), secretReference: auth === "reference" ? String(form.get("secretEnv") ?? "") : auth === "none" ? "" : c.secretReference };
    if (await operate(() => request("/api/connections/configure", { method: "POST", body: JSON.stringify({ connection, apiKey: auth === "key" ? form.get("apiKey") : null }) }), true)) { if (element.querySelector<HTMLInputElement>('input[name="apiKey"]')?.value) element.reset(); }
  };
  return <section className="connection-card">
    <div className="connection-card-heading"><div><h3>{c.name}</h3><span dir="ltr" className="connection-url">{c.endpoint}</span></div><span className="badge">{[t("Local"), t("Cloud"), t("Remote")][c.trustZone]}</span></div>
    <p>{providerKinds.find(kind => kind.value === c.kind)?.label}</p>
    {error && <p className="error" role="alert">{t(error)}</p>}
    {notice && <p className="notice" role="status">{t(notice)}</p>}
    <fieldset disabled={working || d.busy} className="connection-controls" aria-busy={working}>
      <div className="form-actions">
        <button type="button" className="secondary" onClick={() => void operate(() => request(`/api/connections/${c.id}/test`, { method: "POST" })).then(success => { if (success) setNotice("Connection reached successfully"); })}>{t("Test connection")}</button>
        <button type="button" className="secondary" onClick={() => void operate(() => request(`/api/connections/${c.id}/discover`, { method: "POST" }), true)}>{t("Discover models")}</button>
        {c.trustZone === 1 && <button type="button" className="secondary" onClick={() => void d.toggleCloudEgress(c)}>{c.cloudEgressAllowed ? t("Disable cloud") : t("Enable cloud")}</button>}
      </div>
      {working && <p role="status">{t("Loading workspace…")}</p>}
      {testOutput && <pre className="model-test-result">{testOutput}</pre>}
      <details><summary>{t("Edit connection")}</summary><ConnectionEditor key={c.endpoint + c.secretReference + c.kind} connection={c} busy={working} onSave={event => void saveConnection(event)} /></details>
      <h3>{t("Models for this connection")}</h3>
      {models.length === 0 && <p>{t("Add model IDs manually, or discover them from the provider.")}</p>}
      <form className="model-add-form" onSubmit={event => void addModel(event)}>
        <label>{t("Model ID")}<input name="modelId" dir="ltr" required placeholder="provider/model-id" /></label>
        <label>{t("Display name")}<input name="displayName" /></label>
        <label className="check-label"><input name="isDefault" type="checkbox" defaultChecked={models.length === 0} />{t("Default model")}</label>
        <button>{t("Add model")}</button>
      </form>
      <div className="model-list">{models.map(model => <form className="model-row" key={model.modelId + model.updatedAt} onSubmit={event => {
        event.preventDefault(); const form = new FormData(event.currentTarget); const enabled = form.get("enabled") === "on";
        void saveModel({ ...model, displayName: String(form.get("displayName") || model.modelId), enabled, isDefault: enabled && form.get("isDefault") === "on" });
      }}>
        <code dir="ltr">{model.modelId}</code>
        <label>{t("Display name")}<input name="displayName" defaultValue={model.displayName} required /></label>
        <div className="model-row-options"><label className="check-label"><input name="enabled" type="checkbox" defaultChecked={model.enabled} />{t("Enabled")}</label>
        <label className="check-label"><input name="isDefault" type="checkbox" defaultChecked={model.isDefault} />{t("Default model")}</label></div>
        <div className="form-actions"><button className="secondary">{t("Save")}</button><button type="button" disabled={!model.enabled} className="secondary" onClick={() => void operate(async () => {
          const response = await request<{ output: string }>(`/api/connections/${c.id}/test-model`, { method: "POST", body: JSON.stringify({ modelId: model.modelId }) }); setTestOutput(response.output);
        })}>{t("Test model")}</button></div>
      </form>)}</div>
      <small>{t("Testing a model sends a short request and may consume provider tokens.")}</small>
    </fieldset>
  </section>;
}
