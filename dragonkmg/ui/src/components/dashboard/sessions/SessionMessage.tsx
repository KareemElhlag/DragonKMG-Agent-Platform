import { useState } from "react";
import { useI18n } from "../../../i18n";
import type { SessionMessage as Message } from "../../../models";
import { presentMessage } from "../../../services/messagePresentation";
import { ChatMarkdown } from "./ChatMarkdown";
import "./message.css";

export function SessionMessage({ message }: { message: Message }) {
  const { t, locale } = useI18n();
  const [copied, setCopied] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);
  const view = presentMessage(message);
  const timestamp = <time dateTime={message.createdAt}>{new Date(message.createdAt).toLocaleTimeString(locale, { hour: "2-digit", minute: "2-digit" })}</time>;
  if (view.kind === "execution") return <details className="chat-run-record"><summary><span>{t("Execution record")} · <bdi>{view.label}</bdi></span>{timestamp}</summary><pre dir="auto">{message.content}</pre></details>;
  const copy = async () => {
    try { await navigator.clipboard.writeText(view.body); setCopied(true); setCopyFailed(false); }
    catch { setCopied(false); setCopyFailed(true); }
  };
  return <article className={`chat-message ${message.role === "user" ? "is-user" : "is-assistant"}`}>
    <header><strong><bdi>{view.label || t(message.role === "user" ? "Session role: user" : message.role === "assistant" ? "Session role: assistant" : "Execution record")}</bdi></strong>{view.localReply && <span className="chat-message-status">{t("Quick local reply · model not called")}</span>}{timestamp}</header>
    <div dir="auto" className="session-chat-text"><ChatMarkdown source={view.body} /></div>
    <footer className="chat-message-actions" aria-label={t("Message actions")}>
      <button type="button" onClick={() => void copy()}>{t(copied ? "Copied" : "Copy message")}</button>
      {view.hasDetails && <details><summary>{t("Run details")}</summary><pre dir="auto">{message.content}</pre></details>}
      {copyFailed && <span role="status">{t("The operation could not be completed.")}</span>}
    </footer>
  </article>;
}
