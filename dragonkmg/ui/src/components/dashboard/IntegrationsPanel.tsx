import { useCallback, useEffect, useState } from "react";
import { useI18n } from "../../i18n";
import { request } from "../../services/api";
import "./integrations.css";

type McpServer = { serverId: string; enabled: boolean; grantCount: number };
type McpTool = { name: string; description?: string | null; inputSchemaJson: string };
type McpStatus = { kind: "current"; servers: McpServer[] } | { kind: "restart"; configuredCount: number } | { kind: "error" };

async function readMcpStatus(): Promise<McpStatus> {
  try { return { kind: "current", servers: await request<McpServer[]>("/api/mcp/servers") }; }
  catch {
    try {
      const readiness = await request<{ mcpServerCount?: number }>("/api/operations/readiness");
      if (typeof readiness.mcpServerCount === "number") return { kind: "restart", configuredCount: readiness.mcpServerCount };
    } catch { /* Keep authentication and connectivity failures distinct from an old API build. */ }
    return { kind: "error" };
  }
}

export function IntegrationsPanel() {
  const { t } = useI18n();
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState<McpStatus>({ kind: "current", servers: [] });
  const [discovering, setDiscovering] = useState<string | null>(null);
  const [discoveryError, setDiscoveryError] = useState<string | null>(null);
  const [toolsByServer, setToolsByServer] = useState<Record<string, McpTool[]>>({});

  const refresh = useCallback(async () => {
    setLoading(true);
    try { setStatus(await readMcpStatus()); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => {
    let active = true;
    readMcpStatus().then(value => {
      if (active) { setStatus(value); setLoading(false); }
    });
    return () => { active = false; };
  }, []);

  const servers = status.kind === "current" ? status.servers : [];
  const loadError = status.kind === "error";

  async function discover(serverId: string) {
    setDiscovering(serverId);
    setDiscoveryError(null);
    try {
      const tools = await request<McpTool[]>(`/api/mcp/${encodeURIComponent(serverId)}/discover`, { method: "POST" });
      setToolsByServer(previous => ({ ...previous, [serverId]: tools }));
    } catch { setDiscoveryError(serverId); }
    finally { setDiscovering(null); }
  }

  return <div className="integration-page">
    <header className="integration-page-heading">
      <div><p className="integration-eyebrow">{t("Platform connections")}</p><h1>{t("Integrations")}</h1><p>{t("Inspect trusted tool servers and discover their available capabilities.")}</p></div>
      <button type="button" className="integration-refresh" onClick={() => void refresh()} disabled={loading}>{t(loading ? "Loading…" : "Refresh status")}</button>
    </header>

    <section className="integration-card mcp-card" aria-labelledby="mcp-title" aria-busy={loading}>
      <div className="integration-card-heading">
        <div><span className="integration-mark" aria-hidden="true">M</span><div><h2 id="mcp-title">Model Context Protocol (MCP)</h2><p>{t("Controlled access between agents and explicitly trusted external tools.")}</p></div></div>
        <span className={`integration-state ${loadError ? "is-error" : servers.some(server => server.enabled) ? "is-ready" : "is-idle"}`} role="status">
          {loadError ? t("Status unavailable") : loading ? t("Loading…") : t(status.kind === "restart" ? "Restart API" : servers.some(server => server.enabled) ? "Runtime ready" : "Setup required")}
        </span>
      </div>

      {loadError ? <div className="integration-empty integration-error" role="alert"><p>{t("Could not load MCP runtime status. Check platform access and retry.")}</p><button type="button" onClick={() => void refresh()}>{t("Retry")}</button></div> : loading ? <div className="integration-loading" role="status">{t("Loading MCP runtime status…")}</div> : status.kind === "restart" ? <div className="integration-empty">
        <strong>{t("Restart API to load the new MCP server list")}</strong>
        <p>{t("The running API is an older build. Its readiness check reports {count} configured MCP server(s), but the new safe server-list endpoint is not loaded yet. Restart the local API, then refresh this page.").replace("{count}", String(status.configuredCount))}</p>
      </div> : servers.length === 0 ? <div className="integration-empty">
        <strong>{t("No MCP servers are configured")}</strong>
        <p>{t("The MCP client is implemented, but this runtime has no trusted server entries yet. Add a server and explicit grants to its local registry, then restart the API.")}</p>
        <code dir="ltr">DRAGONKMG_MCP_REGISTRY=path/to/mcp-registry.json</code>
        <small dir="ltr">dragonkmg/docs/MCP_INTEGRATION.ar.md</small>
      </div> : <>
        <div className="integration-server-list">
          {servers.map(server => <article className="integration-server" key={server.serverId}>
            <div className="integration-server-main">
              <div><strong dir="ltr">{server.serverId}</strong><span className={`integration-server-state ${server.enabled ? "is-ready" : "is-idle"}`}>{t(server.enabled ? "Enabled" : "Disabled")}</span></div>
              <p>{t("{count} explicit tool grants").replace("{count}", String(server.grantCount))}</p>
            </div>
            <button type="button" onClick={() => void discover(server.serverId)} disabled={!server.enabled || discovering !== null}>{t(discovering === server.serverId ? "Discovering tools…" : "Discover tools")}</button>
            {discoveryError === server.serverId && <p className="integration-inline-error" role="alert">{t("Discovery failed. Verify that the trusted server is reachable and try again.")} <button type="button" onClick={() => void discover(server.serverId)} disabled={discovering !== null}>{t("Retry")}</button></p>}
            {toolsByServer[server.serverId] && <div className="integration-tools" aria-live="polite">
              <h3>{t("Discovered tools")} <span>{toolsByServer[server.serverId].length}</span></h3>
              {toolsByServer[server.serverId].length === 0 ? <p>{t("This server returned no tools.")}</p> : <ul>{toolsByServer[server.serverId].map(tool => <li key={tool.name}><strong dir="ltr">{tool.name}</strong>{tool.description && <p>{tool.description}</p>}<details><summary>{t("Input schema")}</summary><pre dir="ltr">{tool.inputSchemaJson}</pre></details></li>)}</ul>}
            </div>}
          </article>)}
        </div>
        <p className="integration-security-note">{t("Discovery only lists tools; this screen cannot execute them. Calls still require an enabled agent, an explicit per-tool grant, and the required API scope.")}</p>
      </>}
      <div className="integration-capabilities"><span>{t("Streamable HTTP")}</span><span>{t("JSON + SSE")}</span><span>{t("Schema validation")}</span><span>{t("Explicit grants")}</span><span>{t("Redirects blocked")}</span></div>
    </section>

    <section className="integration-card integration-planned">
      <div><h2>n8n Webhook</h2><span className="integration-state is-idle">{t("Not configured")}</span></div>
      <p>{t("The webhook adapter enforces HTTPS, an allowlist, approval and idempotency. Live endpoint configuration is not exposed in this build.")}</p>
      <span>{t("Planned: LinkedIn and Gmail")}</span>
    </section>
  </div>;
}
