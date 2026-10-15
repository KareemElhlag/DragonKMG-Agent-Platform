import { useI18n } from "../../i18n";
export function IntegrationsPanel() {
  const { t } = useI18n();
  return (
    <div className="grid">
      {[
        [
          "MCP",
          "MCP transport is not connected yet. Tool grants and trusted-server policies are available in the backend.",
        ],
        [
          "n8n Webhook",
          "The n8n connector enforces HTTPS, allowlist, approval and idempotency. Live webhook configuration is not exposed yet.",
        ],
      ].map(([name, description]) => (
        <section className="panel" key={name}>
          <h2>{name}</h2>
          <span className="badge">{t("Configuration required")}</span>
          <p>
            {t(
              description as "MCP transport is not connected yet. Tool grants and trusted-server policies are available in the backend.",
            )}
          </p>
        </section>
      ))}
    </div>
  );
}
