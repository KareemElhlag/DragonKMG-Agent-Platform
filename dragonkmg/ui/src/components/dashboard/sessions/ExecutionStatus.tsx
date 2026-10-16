import { useI18n } from "../../../i18n";
import type { ExecutionEvent } from "../../../hooks/useSessionExecution";
import "./execution.css";
const labels = { validating_files: "Execution: validating context", running_agent: "Execution: agent running", agent_completed: "Execution: agent finished", completed: "Execution: completed", failed: "Execution: failed", cancelled: "Execution: cancelled" } as const;
export function ExecutionStatus({ events, sending }: { events: ExecutionEvent[]; sending: boolean }) {
  const { t } = useI18n();
  if (!sending && events.length === 0) return null;
  const last = events.at(-1);
  const label = (value: ExecutionEvent) => t(labels[value.phase as keyof typeof labels] ?? "Execution: awaiting status");
  return <details className="chat-execution"><summary><span role="status">{last ? label(last) : t("Execution: awaiting status")}{last?.agentName && <> · <bdi>{last.agentName}</bdi> · {last.member}/{last.total}</>}</span></summary><ol>{events.map((event, index) => <li key={index}>{label(event)}{event.agentName && <> · <bdi>{event.agentName}</bdi> · <code dir="ltr">{event.modelId}</code> · {event.member}/{event.total}</>}</li>)}</ol><p>{t("Execution: real status only, not private reasoning")}</p></details>;
}
