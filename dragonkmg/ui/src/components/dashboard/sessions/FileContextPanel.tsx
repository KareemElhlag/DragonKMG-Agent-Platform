import { useI18n, systemTextKey } from "../../../i18n";
import type { SessionChatController } from "../SessionsPanel";

export function FileContextPanel({ chat, open, close }: { chat: SessionChatController; open: boolean; close: () => void }) {
  const { t } = useI18n();
  const hasReferences = /^\s*(?:\/file|@file)\s+/im.test(chat.draft);
  return <>
    {!open && chat.fileError && <p role="alert" className="error">{t(systemTextKey(chat.fileError) ? chat.fileError : "FileUnavailable")}</p>}
    {open && <section className="chat-file-context" aria-label={t("Project file context")}>
      <header><h3>{t("Project file context")}</h3><button type="button" onClick={close}>{t("Close")}</button></header>
      <p>{t("Preview a text file from the selected project. This does not upload a file from your device.")}</p>
      <form onSubmit={event => { event.preventDefault(); void chat.previewFile(); }}><label>{t("File path")}<input value={chat.filePath} onChange={event => chat.setFilePath(event.target.value)} dir="ltr" maxLength={2000} required disabled={chat.busy || chat.fileBusy} placeholder="src/example.cs" /></label><button type="submit" disabled={chat.busy || chat.fileBusy || !chat.sessionId || !chat.projectId || !chat.filePath.trim()}>{t(chat.fileBusy ? "Reading file…" : "Preview file")}</button></form>
      {!chat.projectId && <p role="status">{t("ProjectRequired")}</p>}
      {chat.fileError && <p role="alert" className="error">{t(systemTextKey(chat.fileError) ? chat.fileError : "FileUnavailable")}</p>}
      {chat.filePreview && <div className="chat-file-preview"><header><bdi>{chat.filePreview.relativePath}</bdi><span>{chat.filePreview.bytes.toLocaleString()} {t("bytes")}</span></header><details><summary>{t("Preview contents")}</summary><pre dir="auto">{chat.filePreview.content}</pre><small dir="ltr">SHA-256: {chat.filePreview.sha256}</small></details><button type="button" disabled={chat.busy || chat.fileBusy} onClick={chat.addFileReference}>{t("Add file reference to prompt")}</button></div>}
      <small>{t('Add /file "path" on its own line for each selected file. Only explicit references are read.')}</small>
    </section>}
    {(hasReferences || chat.allowFileEgress) && <label className="chat-file-egress"><input type="checkbox" checked={chat.allowFileEgress} disabled={chat.busy || chat.fileBusy} onChange={event => chat.setAllowFileEgress(event.target.checked)} /><span>{t("Allow selected file contents to be sent to the team model providers for this request")}</span></label>}
  </>;
}
