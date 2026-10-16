import { useTranscriptScroll } from "../../../hooks/useTranscriptScroll";
import { useI18n, systemTextKey } from "../../../i18n";
import type { SessionChatController } from "../SessionsPanel";
import { DragonLogo } from "../../ui/DragonLogo";
import { SessionMessage } from "./SessionMessage";

export function SessionMessages({ chat }: { chat: SessionChatController }) {
  const { t } = useI18n();
  const { transcript, content, showLatest, onScroll, jumpToLatest } = useTranscriptScroll(chat.sessionId, chat.messages);
  return <div className="chat-message-area"><div ref={transcript} className="chat-messages" aria-label={t("Chat history")} aria-busy={chat.loading} onScroll={onScroll}><div ref={content} className="chat-transcript-content">
    {chat.historyHasMore && <button type="button" className="chat-load-older" disabled={chat.historyLoadingOlder || chat.loading} onClick={() => void chat.loadOlder()}>{t(chat.historyLoadingOlder ? "Loading history…" : "Load older messages")}</button>}
    {chat.historyError && <div className="alert error" role="alert">{t(systemTextKey(chat.historyError) ? chat.historyError : "Conversation history could not be loaded. Retry without creating a new session.")}<button type="button" disabled={chat.loading} onClick={() => void chat.refresh()}>{t("Retry")}</button></div>}
    {chat.loading && chat.messages.length === 0 ? <p className="chat-loading" role="status">{t("Loading history…")}</p> : chat.historyError && chat.messages.length === 0 ? null : !chat.sessionId || chat.messages.length === 0 ? <div className="chat-welcome"><DragonLogo size={64} /><span className="chat-eyebrow">DragonKMG</span><h2>{t(chat.sessionId ? "What are we working on?" : "Your workspace, in your language")}</h2><p>{t(chat.sessionId ? "Choose your team, write your request, and follow the real results here." : "Select or create a session to start. Arabic text is shaped naturally here.")}</p></div> : chat.messages.map(message => <div key={message.id} data-scroll-id={message.id}><SessionMessage message={message} /></div>)}
    {chat.sending && <p className="chat-working" role="status"><span className="status-dot" />{t("Waiting for the provider…")}</p>}
  </div></div>{showLatest && <button type="button" className="chat-jump-latest" onClick={jumpToLatest}>{t("Jump to latest")}</button>}</div>;
}
