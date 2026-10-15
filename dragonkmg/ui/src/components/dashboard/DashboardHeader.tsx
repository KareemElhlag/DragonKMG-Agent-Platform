import type { DashboardController } from "../../hooks/useDashboard";
import { useI18n } from "../../i18n";
import type { TranslationKey } from "../../i18n";
import { useTheme } from "../../hooks/useTheme";
import { Icon } from "../ui/Icon";
import { LanguagePicker } from "./LanguagePicker";
import { DragonLogo } from "../ui/DragonLogo";

export function DashboardHeader({
  dashboard,
  section,
}: {
  dashboard: DashboardController;
  section: TranslationKey;
}) {
  const { t } = useI18n();
  const { theme, toggleTheme } = useTheme();
  const nextTheme =
    theme === "dark" ? t("Switch to light mode") : t("Switch to dark mode");
  return (
    <header className="dashboard-header">
      <div className="header-copy">
        <div className="header-breadcrumb">
          <DragonLogo size={22} />
          <span dir="ltr">DragonKMG</span>
          <span aria-hidden="true">/</span>
          <span>{t("Workspace")}</span>
        </div>
        <h1>{t(section)}</h1>
        <p>{t("subtitle")}</p>
      </div>
      <div className="header-actions">
        <LanguagePicker />
        <div className="appearance-actions">
          <button
            type="button"
            className="header-control icon-button"
            onClick={toggleTheme}
            aria-label={nextTheme}
            title={nextTheme}
          >
            <Icon name={theme === "dark" ? "sun" : "moon"} />
          </button>
          <span className="control-divider" aria-hidden="true" />
          <button
            type="button"
            className="header-control refresh-button"
            disabled={dashboard.busy || dashboard.loading}
            onClick={() => void dashboard.load()}
            aria-label={
              dashboard.loading ? t("Loading workspace…") : t("Refresh")
            }
            title={t("Refresh")}
          >
            <Icon
              name="refresh"
              className={dashboard.loading ? "is-spinning" : ""}
            />
            <span>{t("Refresh")}</span>
          </button>
        </div>
      </div>
    </header>
  );
}
