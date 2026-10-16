import { Fragment, useMemo, useState } from "react";
import { useI18n } from "../../../i18n";
import type { DashboardController } from "../../../hooks/useDashboard";
import type { SessionChatController } from "../SessionsPanel";
export function SessionSidebar({ dashboard, chat, open, close }: { dashboard: DashboardController; chat: SessionChatController; open: boolean; close: () => void }) {
  const { t, locale } = useI18n();
  const [search, setSearch] = useState(""); const [name, setName] = useState("");
  const sessions = useMemo(() => [...dashboard.sessions].filter(session => session.name.toLocaleLowerCase(locale).includes(search.toLocaleLowerCase(locale))).sort((a, b) => Date.parse(b.lastOpenedAt) - Date.parse(a.lastOpenedAt)), [dashboard.sessions, search, locale]);
  return <aside id="session-history-sidebar" className={`chat-history-sidebar${open ? " is-open" : ""}`} aria-label={t("Chat history")}>
    <div className="chat-history-heading"><h2>{t("Your conversations")}</h2><button type="button" className="chat-history-close" onClick={close}>{t("Close")}</button></div>
    <details className="chat-new-session"><summary>{t("New session")}</summary><form onSubmit={event => { event.preventDefault(); void chat.create(name); }}><label>{t("Session name")}<input value={name} onChange={event => setName(event.target.value)} required maxLength={120} disabled={chat.busy} /></label><button type="submit" disabled={chat.busy || !name.trim()}>{t("Create session")}</button></form></details>
    <label className="chat-search">{t("Search conversations")}<input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder={t("Search by session name")} /></label>
    <nav className="chat-session-list" aria-label={t("Saved conversations")}>{sessions.length === 0 ? <p className="chat-sidebar-empty">{t(search ? "No matching conversations" : "No saved conversations yet")}</p> : sessions.map((session, index) => {
      const month = new Date(session.lastOpenedAt).toLocaleDateString(locale, { month: "long", year: "numeric" });
      const previousMonth = index > 0 ? new Date(sessions[index - 1].lastOpenedAt).toLocaleDateString(locale, { month: "long", year: "numeric" }) : "";
      return <Fragment key={session.id}>{month !== previousMonth && <h3 className="chat-session-group">{month}</h3>}<button type="button" disabled={chat.busy} className={`chat-session-item${chat.sessionId === session.id ? " is-selected" : ""}`} aria-current={chat.sessionId === session.id ? "page" : undefined} onClick={() => { chat.selectSession(session.id); close(); }}><strong dir="auto">{session.name}</strong><time dateTime={session.lastOpenedAt}>{new Date(session.lastOpenedAt).toLocaleDateString(locale, { month: "short", day: "numeric" })}</time></button></Fragment>;
    })}</nav>
    <footer>{t("Saved locally · shared with the terminal")}</footer>
  </aside>;
}
