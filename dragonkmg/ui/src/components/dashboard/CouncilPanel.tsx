import { CouncilSetup } from "./CouncilSetup";
import { Panel } from "../ui/Panel";
import { useI18n, systemTextKey } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";

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
  return (
    <Panel
      title="Default multi-agent council"
      caption="Creates Execution, Quality Review, and Security Test agents once. Each receives the four default skills; assignments stay editable."
    >
      <CouncilSetup dashboard={dashboard} />
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
