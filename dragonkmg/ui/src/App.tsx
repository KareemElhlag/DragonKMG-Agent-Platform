import { useState } from "react";
import { useI18n } from "./i18n";
import { I18nProvider } from "./I18nProvider";
import { useDashboard } from "./hooks/useDashboard";
import { DashboardHeader } from "./components/dashboard/DashboardHeader";
import { SessionsPanel } from "./components/dashboard/SessionsPanel";
import { AgentsPanel } from "./components/dashboard/AgentsPanel";
import { TeamPanel } from "./components/dashboard/TeamPanel";
import { AgentTestPanel } from "./components/dashboard/AgentTestPanel";
import { ConnectionsPanel } from "./components/dashboard/ConnectionsPanel";
import { RolesPanel } from "./components/dashboard/RolesPanel";
import { SkillsPanel } from "./components/dashboard/SkillsPanel";
import { AssignmentsPanel } from "./components/dashboard/AssignmentsPanel";
import { CouncilPanel } from "./components/dashboard/CouncilPanel";
import { AccessSettings } from "./components/dashboard/AccessSettings";
import { PublicKeySettings } from "./components/dashboard/PublicKeySettings";
import { SecurityPanel } from "./components/dashboard/SecurityPanel";
import { IntegrationsPanel } from "./components/dashboard/IntegrationsPanel";
import { Sidebar } from "./components/dashboard/Sidebar";
import type { DashboardSection } from "./components/dashboard/Sidebar";
import "./index.css";

function Dashboard() {
  const d = useDashboard();
  const { t } = useI18n();
  const [active, setActive] = useState<DashboardSection>(() => location.hash === "#sessions" ? "Sessions" : "Agents");
  return (
    <div className={"app-shell" + (active === "Sessions" && !d.authRequired ? " sessions-mode" : "")}>
      <Sidebar active={active} onSelect={setActive} />
      <main id="workspace">
        <DashboardHeader dashboard={d} section={active} />
        {!d.authRequired && <div className="overview">
          <div>
            <strong>{d.agents.length}</strong>
            <span>{t("Agents")}</span>
          </div>
          <div>
            <strong>{d.connections.length}</strong>
            <span>{t("Connections")}</span>
          </div>
          <div>
            <strong>{d.skills.length}</strong>
            <span>{t("Skills")}</span>
          </div>
        </div>}
        {d.error && !d.authRequired && (
          <div className="alert error" role="alert">
            {t(d.error)}
            <button disabled={d.loading} onClick={() => void d.load()}>
              {t("Retry")}
            </button>
          </div>
        )}
        {!d.error && !d.authRequired && (
          <p className="notice" role="status">
            {t(d.notice)}
          </p>
        )}
        <fieldset
          className="workspace-content"
          disabled={d.busy || d.loading}
          aria-busy={d.busy || d.loading}
        >
          {d.authRequired ? <section className="auth-recovery" aria-label={t("Local API token")}>
            {d.error && <p className="error" role="alert">{t(d.error)}</p>}
            <AccessSettings dashboard={d} />
          </section> : <>
          {active === "Agents" && (
            <div className="agents-workspace">
              <TeamPanel dashboard={d} />
              <div className="grid">
                <AgentsPanel dashboard={d} />
                <AgentTestPanel dashboard={d} />
              </div>
            </div>
          )}
          {active === "Connections" && (
            <div className="grid">
              <ConnectionsPanel dashboard={d} />
              <AccessSettings dashboard={d} />
            </div>
          )}
          {active === "Skills" && (
            <div className="grid">
              <SkillsPanel dashboard={d} />
              <AssignmentsPanel dashboard={d} />
            </div>
          )}
          {active === "Workflows" && <CouncilPanel dashboard={d} />}
          {active === "Sessions" && <SessionsPanel dashboard={d} />}
          {active === "Integrations" && <IntegrationsPanel />}
          {active === "Security" && <SecurityPanel />}
          {active === "Identity" && (
            <div className="grid">
              <RolesPanel dashboard={d} />
              <PublicKeySettings />
            </div>
          )}
          </>}
        </fieldset>
      </main>
    </div>
  );
}
export default function App() {
  return (
    <I18nProvider>
      <Dashboard />
    </I18nProvider>
  );
}
