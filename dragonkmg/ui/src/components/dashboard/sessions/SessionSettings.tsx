import { useI18n } from "../../../i18n";
import type { SessionChatController } from "../SessionsPanel";
export function SessionSettings({ chat }: { chat: SessionChatController }) {
  const { t } = useI18n();
  return <details className="chat-session-settings"><summary>{t("Team settings")}<span><bdi>{chat.teams.find(team => team.id === (chat.teamId || chat.defaultTeamId))?.name ?? t("No default team")}</bdi></span></summary><div className="chat-settings-content">
    <section><h3>{t("Request team")}</h3><label>{t("Request team")}<select value={chat.teamId} disabled={chat.busy || chat.teamsLoading} onChange={event => chat.setTeamId(event.target.value)}><option value="">{t("Use session default")}</option>{chat.teams.map(team => <option key={team.id} value={team.id} disabled={!team.enabled}>{team.name}</option>)}</select></label><div className="form-actions"><button type="button" disabled={chat.busy || chat.loading || !chat.sessionId || !chat.teamId} onClick={() => void chat.saveDefaultTeam()}>{t("Save session default")}</button><button type="button" disabled={chat.busy || chat.loading || !chat.sessionId || !chat.defaultTeamId} onClick={() => void chat.saveDefaultTeam(true)}>{t("Clear session default")}</button></div><p role="status">{t("Session default")}: <bdi>{chat.teams.find(team => team.id === chat.defaultTeamId)?.name ?? t("No default team")}</bdi>{chat.teamSaved && ` · ${t("Team saved")}`}</p><small>{t("Use /team followed by a team name or ID, then a new line and your prompt.")}</small></section>
  </div></details>;
}
