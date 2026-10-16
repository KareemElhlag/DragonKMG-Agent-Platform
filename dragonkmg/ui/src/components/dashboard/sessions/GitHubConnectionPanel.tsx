import { useState } from "react";
import { useI18n } from "../../../i18n";
import { useGitHubConnection } from "../../../hooks/useGitHubConnection";
import "./github-connection.css";

export function GitHubConnectionPanel({ projectId }: { projectId: string }) {
  const { t } = useI18n();
  const github = useGitHubConnection(projectId);
  const [token, setToken] = useState("");
  return <section className="github-connection" aria-label={t("GitHub account")} aria-busy={github.busy}>
    <div className="github-connection-heading"><strong>GitHub</strong>{github.status?.connected && <bdi dir="ltr">@{github.status.login}</bdi>}</div>
    {github.error && <div role="alert" className="github-feedback error"><p>{t(github.error === "invalid" ? "GitHub token validation failed. Check the token and repository permissions." : "GitHub connection failed. Retry or check token permissions.")}</p><button type="button" disabled={github.busy} onClick={() => void github.refresh()}>{t("Retry")}</button></div>}
    {github.busy && <p role="status">{t("Loading…")}</p>}
    {github.status && !github.status.vaultSupported && <p role="status">{t("GitHub Vault is unavailable on this environment.")}</p>}
    {!github.status?.connected && <form onSubmit={event => { event.preventDefault(); const value = token.trim(); setToken(""); if (value) void github.connect(value); }}>
      <label>{t("Fine-grained access token")}<input type="password" value={token} onChange={event => setToken(event.target.value)} autoComplete="off" spellCheck={false} dir="ltr" required maxLength={512} disabled={github.busy || !github.status?.vaultSupported} /></label>
      <div className="github-actions"><button type="submit" disabled={github.busy || !github.status?.vaultSupported || !token.trim()}>{t("Connect GitHub")}</button><a href="https://github.com/settings/personal-access-tokens/new" target="_blank" rel="noopener noreferrer">{t("Create a fine-grained token")}</a></div>
      <p className="github-hint">{t("GitHub token is stored in Windows Vault, never in this browser.")}</p>
    </form>}
    {github.status?.connected && <>
      <div className="github-actions"><button type="button" disabled={github.busy} onClick={() => void github.refresh()}>{t("Refresh")}</button><button type="button" disabled={github.busy} onClick={() => void github.disconnect()}>{t("Disconnect GitHub")}</button></div>
      <h4>{t("GitHub repositories")}</h4>
      {!projectId && <p role="status">{t("Select a project folder first.")}</p>}
      {github.binding && <p role="status">{t("Linked repository")}: <bdi dir="ltr">{github.binding.fullName}</bdi></p>}
      {!github.busy && !github.error && github.repositories.length === 0 && <p>{t("No repositories available")}</p>}
      <ul className="github-repositories">{github.repositories.map(repository => <li key={repository.id}><span><bdi dir="ltr">{repository.fullName}</bdi><small>{t(repository.private ? "Private repository" : "Public repository")}</small></span><button type="button" disabled={github.busy || !projectId || github.binding?.fullName === repository.fullName} onClick={() => void github.bind(repository.fullName)}>{t(github.binding?.fullName === repository.fullName ? "Repository linked" : "Bind repository")}</button></li>)}</ul>
      <nav className="github-actions" aria-label={t("GitHub repositories")}><button type="button" disabled={github.busy || github.page === 1} onClick={github.previous}>{t("Previous page")}</button><span>{t("Page")} {github.page}</span><button type="button" disabled={github.busy || github.page === 100 || github.repositories.length === 0} onClick={github.next}>{t("Next page")}</button></nav>
    </>}
    <p className="github-hint">{t("Connecting GitHub does not grant agents file access or send repository content to models.")}</p>
  </section>;
}
