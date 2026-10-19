import { useI18n } from "../../../i18n";
import type { SessionChatController } from "../SessionsPanel";
import { useLayoutEffect, useRef, useState } from "react";
import { FileContextPanel } from "./FileContextPanel";
import { SessionProjectBar } from "./SessionProjectBar";
import { ExecutionStatus } from "./ExecutionStatus";
import { Icon } from "../../ui/Icon";
import "./composer.css";
export function SessionComposer({ chat, teamName, agents }: { chat: SessionChatController; teamName?: string; agents: { id: string; name: string; enabled: boolean }[] }) {
  const { t } = useI18n();
  const [filesOpen, setFilesOpen] = useState(false);
  const input = useRef<HTMLTextAreaElement>(null);
  const attachment = useRef<HTMLButtonElement>(null);
  const fileContext = useRef<HTMLDivElement>(null);
  useLayoutEffect(() => { if (filesOpen) fileContext.current?.querySelector<HTMLInputElement>("input:not(:disabled)")?.focus(); }, [filesOpen]);
  useLayoutEffect(() => {
    const resize = () => {
      const element = input.current;
      if (!element) return;
      const style = getComputedStyle(element);
      const minimum = Number.parseFloat(style.minHeight) || 64;
      const maximum = Number.parseFloat(style.maxHeight) || 180;
      element.style.height = "0px";
      element.style.height = `${Math.min(maximum, Math.max(minimum, element.scrollHeight))}px`;
      element.style.overflowY = element.scrollHeight > maximum ? "auto" : "hidden";
    };
    resize();
    window.addEventListener("resize", resize);
    return () => window.removeEventListener("resize", resize);
  }, [chat.draft]);
  const closeFiles = () => { setFilesOpen(false); attachment.current?.focus(); };
  return <div className="chat-compose-area" onKeyDown={event => { if (event.key === "Escape" && filesOpen) { event.stopPropagation(); closeFiles(); } }}>
    <ExecutionStatus events={chat.executionEvents} sending={chat.sending} />
    <SessionProjectBar key={chat.sessionId} chat={chat} />
    <div className="chat-execution-mode" role="group" aria-label={t("Execution mode")}>
      <label><span>{t("Run with")}</span><select value={chat.executionMode} disabled={chat.busy} onChange={event => chat.setExecutionMode(event.target.value as "team" | "agent")}><option value="team">{t("Full team")}</option><option value="agent">{t("Single agent")}</option></select></label>
      {chat.executionMode === "agent" ? <label><span>{t("Agent")}</span><select value={chat.agentId} disabled={chat.busy} onChange={event => chat.setAgentId(event.target.value)}><option value="">{t("Choose agent")}</option>{agents.filter(agent => agent.enabled).map(agent => <option key={agent.id} value={agent.id}>{agent.name}</option>)}</select></label> : <span className="chat-mode-hint">{t("All ordered team members will run")}</span>}
    </div>
    <div ref={fileContext} id="session-file-context"><FileContextPanel chat={chat} open={filesOpen} close={closeFiles} /></div>
    <form className="chat-composer" onSubmit={event => { event.preventDefault(); void chat.send(); }}>
      <label htmlFor="session-prompt">{t("Your prompt")}</label>
      <textarea ref={input} id="session-prompt" dir="auto" rows={1} aria-describedby="session-composer-hint" value={chat.draft} disabled={chat.busy || !chat.sessionId} placeholder={t("Write your request here…")} onChange={event => chat.setDraft(event.target.value)} onKeyDown={event => { if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing && event.nativeEvent.keyCode !== 229) { event.preventDefault(); void chat.send(); } }} />
      <div className="chat-composer-actions">
        <div className="chat-composer-context">
          <button ref={attachment} type="button" disabled={chat.busy || !chat.sessionId} aria-expanded={filesOpen} aria-controls="session-file-context" onClick={() => setFilesOpen(!filesOpen)}><span aria-hidden="true" className="chat-attach-symbol">+</span>{t("Attach file")}</button>
          <button type="button" disabled title={t("Permissions are enforced by the engine; this control does not grant access.")}><Icon name="shield" />{t("Permissions")}</button>
          <span className="chat-team-chip" title={teamName ?? t("No default team")}><bdi>{teamName ?? t("No default team")}</bdi></span>
        </div>
        {chat.sending ? <button type="button" className="chat-stop" onClick={chat.cancel}><span aria-hidden="true">■</span>{t("Stop request")}</button> : <button type="submit" className="primary chat-send" disabled={chat.busy || chat.loading || chat.fileBusy || !chat.sessionId || !chat.draft.trim()}>{t("Send message")}<span aria-hidden="true">↑</span></button>}
      </div>
    </form>
    <p id="session-composer-hint" className="chat-composer-hint">{t("Enter to send · Shift+Enter for a new line")}</p>
  </div>;
}
