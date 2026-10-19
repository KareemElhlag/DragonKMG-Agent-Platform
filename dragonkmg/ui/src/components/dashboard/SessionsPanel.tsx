import { useEffect, useState } from "react";
import { useI18n } from "../../i18n";
import type { DashboardController } from "../../hooks/useDashboard";
import { useSessionChat } from "../../hooks/useSessionChat";
import { SessionSidebar } from "./sessions/SessionSidebar";
import { SessionSettings } from "./sessions/SessionSettings";
import { SessionMessages } from "./sessions/SessionMessages";
import { SessionComposer } from "./sessions/SessionComposer";
import { DragonLogo } from "../ui/DragonLogo";
import "./sessions/workspace.css";
import { request } from "../../services/api";
export type SessionChatController = ReturnType<typeof useSessionChat>;
export function SessionsPanel({ dashboard }: { dashboard: DashboardController }) {
  const { t } = useI18n();
  const chat = useSessionChat(dashboard);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [questions, setQuestions] = useState<{ id: string; prompt: string; optionsJson: string; status: number }[]>([]);
  useEffect(() => { if (!chat.sessionId) return; void request<typeof questions>(`/api/sessions/${chat.sessionId}/questions`).then(setQuestions).catch(() => setQuestions([])); }, [chat.sessionId, chat.sending]);
  const answerQuestion = async (question: typeof questions[number], answer: string) => { await request(`/api/sessions/${chat.sessionId}/questions/${question.id}/answer`, { method: "POST", body: JSON.stringify({ answer }) }); setQuestions(values => values.map(item => item.id === question.id ? { ...item, status: 1 } : item)); };
  const session = dashboard.sessions.find(value => value.id === chat.sessionId);
  const team = chat.teams.find(value => value.id === (chat.teamId || chat.defaultTeamId));
  const error = chat.error || chat.teamsError;
  return <section className="chat-workspace" aria-label={t("Session workspace")} onKeyDown={event => { if (event.key === "Escape") setSidebarOpen(false); }}>
    <SessionSidebar dashboard={dashboard} chat={chat} open={sidebarOpen} close={() => setSidebarOpen(false)} />
    <div className="chat-conversation">
      <header className="chat-thread-header"><button type="button" className="chat-history-toggle" aria-expanded={sidebarOpen} aria-controls="session-history-sidebar" onClick={() => setSidebarOpen(!sidebarOpen)}>{t("Chat history")}</button><DragonLogo size={30} /><div className="chat-thread-title"><h2>{session?.name ?? t("Session workspace")}</h2><p><span className="status-dot" /><bdi>{team?.name ?? t("No default team")}</bdi></p></div><button type="button" className="chat-refresh" disabled={chat.busy || chat.loading || !chat.sessionId} onClick={() => void chat.refresh()}>{t("Refresh history")}</button></header>
      <SessionSettings chat={chat} />
      {error && <div className="alert error chat-error" role="alert">{t(error)}<button type="button" disabled={chat.busy || chat.loading} onClick={() => void chat.refresh()}>{t("Retry")}</button></div>}
      {questions.filter(question => question.status === 0).map(question => <section className="chat-question-card" key={question.id}><strong>{question.prompt}</strong><div>{JSON.parse(question.optionsJson).map((option: string) => <button type="button" key={option} onClick={() => void answerQuestion(question, option)}>{option}</button>)}</div></section>)}
      <SessionMessages chat={chat} /><SessionComposer chat={chat} teamName={team?.name} agents={dashboard.agents} />
    </div>
  </section>;
}
