import { useState } from "react";
import type { ConnectionModel } from "../../models";
import { useI18n } from "../../i18n";

export function ModelPicker({ models, connectionId, initialValue = "" }: { models: ConnectionModel[]; connectionId: string; initialValue?: string }) {
  const { t } = useI18n();
  const available = models.filter(model => model.connectionId === connectionId && model.enabled);
  const [modelId, setModelId] = useState(initialValue || available.find(model => model.isDefault)?.modelId || available[0]?.modelId || "");
  const [custom, setCustom] = useState(initialValue ? !available.some(model => model.modelId === initialValue) : available.length === 0);
  return <div className="model-picker">
    <label>{t("Select model")}<select value={custom ? "__custom__" : modelId} required={!custom} onChange={event => {
      if (event.target.value === "__custom__") { setCustom(true); setModelId("") } else { setCustom(false); setModelId(event.target.value) }
    }}>
      <option value="" disabled>{t("Select model")}</option>
      {available.map(model => <option key={model.modelId} value={model.modelId}>{model.displayName}{model.isDefault ? " · " + t("Default model") : ""}</option>)}
      <option value="__custom__">{t("Enter model ID manually")}</option>
    </select></label>
    {custom ? <label>{t("Model ID")}<input name="modelId" dir="ltr" required value={modelId} onChange={event => setModelId(event.target.value)} /></label> : <input type="hidden" name="modelId" value={modelId} />}
  </div>;
}
