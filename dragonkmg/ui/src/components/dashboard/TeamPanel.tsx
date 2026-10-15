import { useI18n, systemTextKey } from "../../i18n";
import { CouncilSetup } from "./CouncilSetup";
import type { DashboardController } from "../../hooks/useDashboard";
export function TeamPanel({
  dashboard: d,
}: {
  dashboard: DashboardController;
}) {
  const { t } = useI18n();
  return (
    <section className="panel team-panel">
      <h2>{t("Default multi-agent council")}</h2>
      <p>
        {t(
          "Creates Execution, Quality Review, and Security Test agents once. Each receives the four default skills; assignments stay editable.",
        )}
      </p>
      <div className="team-grid">
        {d.teamTemplates.map((template) => {
          const agent = d.agents.find((value) => value.name === template.name);
          return (
            <article className="team-member" data-agent-role={template.name === "Execution Agent" ? "execution" : template.name === "Quality Review Agent" ? "quality" : "security"} key={template.roleId}>
              <strong>
                {systemTextKey(template.name)
                  ? t(template.name)
                  : template.name}
              </strong>
              <span className="badge">
                {agent
                  ? agent.enabled
                    ? t("Enabled")
                    : t("Disabled")
                  : t("Awaiting setup")}
              </span>
              <small>
                {agent ? agent.modelId : t("Four default file-based skills")}
              </small>
              {agent && (
                <small>
                  {
                    d.connections.find(
                      (connection) => connection.id === agent.connectionId,
                    )?.name
                  }
                </small>
              )}
            </article>
          );
        })}
      </div>
      <CouncilSetup dashboard={d} />
    </section>
  );
}
