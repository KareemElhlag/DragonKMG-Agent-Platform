import { useRef, useState } from "react";
import { useI18n } from "../../../i18n";
import type { SessionChatController } from "../SessionsPanel";
import "./project-bar.css";
import { GitHubConnectionPanel } from "./GitHubConnectionPanel";

export function SessionProjectBar({ chat }: { chat: SessionChatController }) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const [pluginsOpen, setPluginsOpen] = useState(false);
  const [savedProject, setSavedProject] = useState<{ id: string; name: string } | null>(null);
  const form = useRef<HTMLFormElement>(null);
  const nameInput = useRef<HTMLInputElement>(null);
  const addButton = useRef<HTMLButtonElement>(null);
  const project = chat.projects.find(value => value.id === chat.projectId);
  const blocked = chat.busy || chat.projectBusy || chat.projectsLoading || !chat.sessionId;
  return <section className="chat-project-bar" aria-label={t("Session project")}>
    <div className="chat-project-bar-row">
      <button type="button" className="chat-context-trigger" popoverTarget="chat-project-menu" aria-label={`${t("Project")}: ${project?.name ?? t("No project")}`}><span className="chat-context-name"><bdi>{project?.name ?? t("Project")}</bdi></span><span aria-hidden="true">+</span></button>
      <button type="button" className="chat-context-trigger" popoverTarget="chat-plugin-menu"><span>{t("Plugins and connections")}</span><span aria-hidden="true">+</span></button>
    </div>
    <div id="chat-project-menu" className="chat-context-popover" popover="auto" role="dialog" aria-label={t("Session project")}>
      <div className="chat-context-heading"><strong>{t("Project")}</strong><button type="button" popoverTarget="chat-project-menu" popoverTargetAction="hide" aria-label={t("Close")}>×</button></div>
      <label className="chat-project-picker"><svg className="chat-folder-glyph" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6"><path d="M3 6a2 2 0 0 1 2-2h5l2 3h7a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6Z" /></svg><span>{t("Project folder")}</span><select value={chat.projectId} disabled={blocked} onChange={event => { setSavedProject(null); void chat.setProject(event.target.value); }}><option value="">{t("No project")}</option>{chat.projects.map(value => <option key={value.id} value={value.id}>{value.name}</option>)}</select></label>
      {project && <details className="chat-project-breadcrumb"><summary>{t("Show project path")}</summary><code dir="ltr">{project.rootPath}</code></details>}
      <button type="button" ref={addButton} disabled={blocked} aria-expanded={open} aria-controls="chat-create-project" onClick={() => { setSavedProject(null); setOpen(!open); if (!open) requestAnimationFrame(() => nameInput.current?.focus()); }}>{t("Add project folder")}</button>
      {(chat.projectBusy || chat.projectsLoading) && <span role="status" className="chat-project-progress">{t(chat.projectBusy ? "Saving project…" : "Loading projects…")}</span>}
    {chat.projectError && <div className="chat-project-feedback error" role="alert">{t(chat.projectError)}<button type="button" disabled={chat.projectBusy || chat.projectsLoading} onClick={() => void chat.refreshProjects()}>{t("Retry")}</button></div>}
    {savedProject && savedProject.id === chat.projectId && <p className="chat-project-feedback" role="status">{t("Project created and selected")}: <bdi>{savedProject.name}</bdi></p>}
    {open && <form ref={form} id="chat-create-project" className="chat-create-project" onSubmit={async event => {
      event.preventDefault();
      const fields = new FormData(event.currentTarget);
      const created = await chat.createProject(String(fields.get("projectName") ?? ""), String(fields.get("rootPath") ?? ""));
      if (created) { setSavedProject({ id: created.id, name: created.name }); form.current?.reset(); setOpen(false); addButton.current?.focus(); }
    }} onKeyDown={event => { if (event.key === "Escape") { setOpen(false); addButton.current?.focus(); } }}>
      <label>{t("Project name")}<input ref={nameInput} name="projectName" required maxLength={100} disabled={chat.projectBusy} /></label>
      <label className="chat-create-project-path">{t("Project folder path")}<input name="rootPath" dir="ltr" required maxLength={2000} disabled={chat.projectBusy} /></label>
      <div className="chat-project-form-actions"><button type="submit" disabled={chat.projectBusy || chat.busy}>{t("Create and select project")}</button><button type="button" disabled={chat.projectBusy} onClick={() => { setOpen(false); addButton.current?.focus(); }}>{t("Cancel")}</button></div>
      <p>{t("A project is a folder. Select individual files separately for context.")} {t("Project roots scope explicit file reads. Every selected agent still needs a file-read grant; no automatic access is granted.")}</p>
    </form>}
    </div>
    <div id="chat-plugin-menu" className="chat-context-popover" popover="auto" role="dialog" aria-label={t("Plugins and connections")} onToggle={event => setPluginsOpen(event.currentTarget.matches(":popover-open"))}>
      <div className="chat-context-heading"><strong>{t("Plugins and connections")}</strong><button type="button" popoverTarget="chat-plugin-menu" popoverTargetAction="hide" aria-label={t("Close")}>×</button></div>
      {pluginsOpen && <details className="chat-plugin-section">
        <summary><strong>GitHub</strong><span>{t("Account, repositories and project binding")}</span></summary>
        <GitHubConnectionPanel key={chat.projectId} projectId={chat.projectId} />
      </details>}
      <div className="chat-plugin-mcp"><strong>MCP</strong><span>{t("Runtime status and tool discovery are available in Integrations.")}</span></div>
      <details className="chat-plugin-roadmap"><summary>{t("Planned integrations")}</summary><p>{t("LinkedIn and Gmail are planned for a later sprint. No accounts are connected and no permissions have been granted.")}</p></details>
    </div>
  </section>;
}
