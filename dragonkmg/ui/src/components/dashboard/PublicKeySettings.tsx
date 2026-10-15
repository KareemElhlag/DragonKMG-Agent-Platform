import { useI18n } from "../../i18n";
export function PublicKeySettings() {
  const { t } = useI18n();
  return (
    <section className="panel">
      <h2>{t("Public keys")}</h2>
      <p>
        {t("Public-key identity management is not connected to the API yet.")}
      </p>
      <span className="badge">{t("Configuration required")}</span>
    </section>
  );
}
