import { CouncilSetup } from "./CouncilSetup";
import { Panel } from "../ui/Panel";
import { useI18n, systemTextKey } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";
import { useEffect, useState } from "react";
import { request } from "../../services/api";
import "./workflow-canvas.css";

type Workflow = { id: string; name: string; description: string; enabled: boolean; version: number };
type Stage = { id: string; workflowId: string; name: string; agentId: string; position: number; checklistJson: string; expectedEvidence: string; requiresApproval: boolean };
type Edge = { workflowId: string; fromStageId: string; toStageId: string };

export function CouncilPanel({
  dashboard,
}: {
  dashboard: DashboardController;
}) {
  const { t } = useI18n();
  const localizeSystemText = (value: string) =>
    systemTextKey(value) ? t(value) : value;
  const {
    busy,
    councilInput,
    councilRun,
    workflow,
    runCouncil,
    setCouncilInput,
  } = dashboard;
  const [workflows, setWorkflows] = useState<Workflow[]>([]);
  const [selectedWorkflow, setSelectedWorkflow] = useState<string>("");
  const [stages, setStages] = useState<Stage[]>([]);
  const [edges, setEdges] = useState<Edge[]>([]);
  const [workflowError, setWorkflowError] = useState("");
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [dragged, setDragged] = useState<string | null>(null);
  useEffect(() => { void request<Workflow[]>("/api/workflows").then(values => { setWorkflows(values); setSelectedWorkflow(values[0]?.id ?? ""); }).catch(() => setWorkflowError("Workflow could not be loaded.")); }, []);
  useEffect(() => { if (!selectedWorkflow) return; void request<{ workflow: Workflow; stages: Stage[]; edges: Edge[] }>(`/api/workflows/${selectedWorkflow}`).then(value => { setStages(value.stages.sort((a,b) => a.position-b.position)); setEdges(value.edges); }).catch(() => setWorkflowError("Workflow could not be loaded.")); }, [selectedWorkflow]);
  const moveStage = (from: string, to: string) => { if (!editing || from === to) return; const next = [...stages]; const a = next.findIndex(item => item.id === from); const b = next.findIndex(item => item.id === to); if (a < 0 || b < 0) return; const [item] = next.splice(a, 1); next.splice(b, 0, item); setStages(next.map((stage, index) => ({ ...stage, position: index }))); };
  const saveWorkflow = async () => { if (!selectedWorkflow || !stages.length || saving) return; setSaving(true); setWorkflowError(""); try { await request(`/api/workflows/${selectedWorkflow}`, { method: "PUT", body: JSON.stringify({ workflow: workflows.find(item => item.id === selectedWorkflow), stages, edges }) }); setEditing(false); } catch { setWorkflowError("Workflow could not be loaded."); } finally { setSaving(false); } };
  return (
    <Panel
      title="Default multi-agent council"
      caption="Creates Execution, Quality Review, and Security Test agents once. Each receives the four default skills; assignments stay editable."
    >
      <CouncilSetup dashboard={dashboard} />
      <div className="workflow-editor-toolbar"><label>{t("Workflow")}<select value={selectedWorkflow} onChange={event => { setEditing(false); setSelectedWorkflow(event.target.value); }}><option value="">{t("Choose workflow")}</option>{workflows.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><span className="workflow-version">{stages.length} {t("stages")} · {edges.length} {t("stages")} links · v{workflows.find(item => item.id === selectedWorkflow)?.version ?? 0}</span><button type="button" onClick={() => setEditing(!editing)} disabled={!selectedWorkflow || saving}>{editing ? t("Cancel") : t("Edit workflow")}</button>{editing && <button type="button" className="primary" onClick={() => void saveWorkflow()} disabled={saving}>{saving ? t("Saving…") : t("Save workflow")}</button>}</div>
      {workflowError && <p className="alert error" role="alert">{t(workflowError as never)}</p>}
      {stages.length > 0 && <div className="workflow-canvas" aria-label={t("Visual workflow editor")}>
        {stages.map((stage, index) => <div className="workflow-node-wrap" key={stage.id} onDragOver={event => { if (editing) event.preventDefault(); }} onDrop={() => { if (dragged) moveStage(dragged, stage.id); setDragged(null); }}><article className={"workflow-node" + (editing ? " is-editing" : "")} draggable={editing} onDragStart={() => setDragged(stage.id)}><span className="workflow-node-index">{index + 1}</span><div><strong>{stage.name}</strong><small>{dashboard.agents.find(agent => agent.id === stage.agentId)?.name ?? stage.agentId}</small></div>{stage.requiresApproval && <em>{t("Approval required")}</em>}</article>{index < stages.length - 1 && <span className="workflow-connector" aria-hidden="true">↓</span>}</div>)}
      </div>}
      <div className="workflow">
        {workflow.map((step, index) => (
          <div className="workflow-step" key={step.name}>
            <strong>
              {index + 1}. {localizeSystemText(step.name)}
            </strong>
            <em>{localizeSystemText(step.owner)}</em>
            <ul>
              {step.checklist.map((item) => (
                <li key={item}>{localizeSystemText(item)}</li>
              ))}
            </ul>
            <span>
              {t("Evidence:")} {localizeSystemText(step.expectedEvidence)}
            </span>
          </div>
        ))}
      </div>
      <textarea
        value={councilInput}
        aria-label={t(
          "Task for execution, quality review, and security review",
        )}
        onChange={(event) => setCouncilInput(event.target.value)}
        placeholder={t(
          "Task for execution, quality review, and security review",
        )}
      />
      <button
        onClick={() => void runCouncil()}
        disabled={busy || !councilInput.trim()}
      >
        {t("Run council workflow")}
      </button>
      {councilRun && (
        <p className="notice">
          {t("Evidence runs:")} {councilRun.executionRunId.slice(0, 8)} →{" "}
          {councilRun.qualityReviewRunId.slice(0, 8)} →{" "}
          {councilRun.securityReviewRunId.slice(0, 8)}
        </p>
      )}
    </Panel>
  );
}
