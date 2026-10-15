import { Panel } from "../ui/Panel";
import { List } from "../ui/List";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";

export function AssignmentsPanel({
  dashboard,
}: {
  dashboard: DashboardController;
}) {
  const { t } = useI18n();
  const {
    skills,
    agents,
    busy,
    selectedAgentId,
    selectedSkillId,
    assignments,
    toolGrants,
    toolIds,
    selectAgent,
    assignSkill,
    removeSkill,
    saveToolGrants,
    setSelectedSkillId,
    setToolIds,
  } = dashboard;
  return (
    <Panel
      title="Agent skills and permissions"
      caption="Skills never grant tools by themselves. Tool-backed skills are blocked unless every required tool is explicitly granted."
    >
      <select
        value={selectedAgentId}
        aria-label={t("Select agent")}
        disabled={busy}
        onChange={(event) => void selectAgent(event.target.value)}
      >
        <option value="">{t("Select agent")}</option>
        {agents.map((x) => (
          <option key={x.id} value={x.id}>
            {x.name}
          </option>
        ))}
      </select>
      <div className="assign">
        <select
          value={selectedSkillId}
          aria-label={t("Select skill")}
          onChange={(event) => setSelectedSkillId(event.target.value)}
        >
          <option value="">{t("Select skill")}</option>
          {skills.map((x) => (
            <option key={x.id} value={x.id}>
              {x.name}
            </option>
          ))}
        </select>
        <button
          onClick={() => void assignSkill()}
          disabled={busy || !selectedAgentId || !selectedSkillId}
        >
          {t("Assign skill")}
        </button>
      </div>
      <List
        values={assignments}
        render={(x) => (
          <>
            <strong>
              {skills.find((skill) => skill.id === x.skillId)?.name ??
                x.skillId}
            </strong>
            <span>
              {t("Priority")} {x.priority} ·{" "}
              {x.enabled ? t("Enabled") : t("Disabled")}
            </span>
            <button onClick={() => void removeSkill(x.skillId)} disabled={busy}>
              {t("Remove")}
            </button>
          </>
        )}
      />
      <h3>{t("Tool grants")}</h3>
      <input
        value={toolIds}
        aria-label={t("Tool grants")}
        onChange={(event) => setToolIds(event.target.value)}
        placeholder={t("Comma-separated tool IDs")}
        disabled={!selectedAgentId}
      />
      <button
        onClick={() => void saveToolGrants()}
        disabled={busy || !selectedAgentId}
      >
        {t("Save tool grants")}
      </button>
      {toolGrants.length > 0 && (
        <p className="empty">
          {toolGrants.length}{" "}
          {t("explicit grant(s), all side effects require approval.")}
        </p>
      )}
    </Panel>
  );
}
