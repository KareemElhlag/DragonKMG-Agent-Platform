import { useEffect, useRef } from "react";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";
import { useSessionChat } from "../../hooks/useSessionChat";
import { DragonLogo } from "../ui/DragonLogo";

export function SessionsPanel({ dashboard }: { dashboard: DashboardController }) {
  const { t, locale } = useI18n();
  const chat = useSessionChat(dashboard);
  const end = useRef<HTMLDivElement>(null);
  const agent = dashboard.agents.find(value => value.id === chat.agentId);
  useEffect(() => { end.current?.scrollIntoView({ block: "nearest" }); }, [chat.messages.length, chat.busy]);
  return <section className="session-chat" aria-label={t("Sessions")}>
    <header className="session-chat-header"><DragonLogo size={36} /><div><h2>{t("Session workspace")}</h2><p>{t("Terminal and dashboard share the same persisted sessions and chat history.")}</p></div></header>
    <div className="session-chat-toolbar">
      <label>{t("Select session")}<select value={chat.sessionId} disabled={chat.busy} onChange={event => chat.selectSession(event.target.value)}><option value="">{t("Select session")}</option>{dashboard.sessions.map(value => <option key={value.id} value={value.id}>{value.name}</option>)}</select></label>
      <label>{t("Select agent")}<select value={chat.agentId} disabled={chat.busy} onChange={event => chat.setAgentId(event.target.value)}><option value="">{t("Select agent")}</option>{dashboard.agents.map(value => <option key={value.id} value={value.id} disabled={!value.enabled}>{value.name} · {value.modelId}</option>)}</select></label>
      <button type="button" disabled={chat.busy || chat.loading || !chat.sessionId} onClick={() => void chat.refresh()}>{t("Refresh history")}</button>
    </div>
    <details className="session-chat-create"><summary>{t("New session")}</summary><form onSubmit={event => { event.preventDefault(); const form = new FormData(event.currentTarget); void chat.create(String(form.get("sessionName") ?? "")); }}><label>{t("Session name")}<input name="sessionName" required maxLength={120} disabled={chat.busy} /></label><button type="submit" disabled={chat.busy}>{t("Create session")}</button></form></details>
    {chat.error && <div className="alert error" role="alert">{t(chat.error)}<button type="button" disabled={chat.busy || chat.loading} onClick={() => void chat.refresh()}>{t("Refresh history")}</button></div>}
    <div className="session-chat-transcript" aria-label={t("Chat history")} aria-busy={chat.loading}>
      {chat.loading ? <p role="status">{t("Loading history…")}</p> : !chat.sessionId ? <div className="session-chat-empty"><DragonLogo size={56} /><h3>{t("Your workspace, in your language")}</h3><p>{t("Select or create a session to start. Arabic text is shaped naturally here.")}</p></div> : chat.messages.length === 0 ? <div className="session-chat-empty"><h3>{t("Start a conversation")}</h3><p>{t("Messages and real provider responses appear above your prompt.")}</p></div> : chat.messages.map(message => <article key={message.id} className={`session-chat-message ${message.role === "user" ? "is-user" : "is-assistant"}`}><header><strong>{message.role === "user" ? t("Session role: user") : message.role === "assistant" ? t("Session role: assistant") : message.role}</strong><time dateTime={message.createdAt}>{new Date(message.createdAt).toLocaleString(locale)}</time></header><div dir="auto" className="session-chat-text">{message.content}</div></article>)}
      {chat.sending && <p className="session-chat-pending" role="status">{t("Waiting for the provider…")}</p>}<div ref={end} />
    </div>
    <form className="session-chat-composer" onSubmit={event => { event.preventDefault(); void chat.send(); }}>
      <label htmlFor="session-prompt">{t("Your prompt")}</label>
      <textarea id="session-prompt" dir="auto" rows={3} value={chat.draft} disabled={chat.busy || !chat.sessionId || !agent?.enabled} placeholder={t("Write your request here…")} onChange={event => chat.setDraft(event.target.value)} onKeyDown={event => { if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing && event.nativeEvent.keyCode !== 229) { event.preventDefault(); void chat.send(); } }} />
      <div className="session-chat-composer-footer"><span><bdi>{agent ? `${agent.name} · ${agent.modelId}` : t("Add an enabled agent to start chatting.")}</bdi><small>{t("Enter to send · Shift+Enter for a new line")}</small></span>{chat.sending ? <button type="button" onClick={chat.cancel}>{t("Stop request")}</button> : <button type="submit" className="primary" disabled={chat.busy || chat.loading || !chat.sessionId || !agent?.enabled || !chat.draft.trim()}>{t("Send message")}</button>}</div>
    </form>
  </section>;
}
