import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const panel = (await Promise.all(["SessionsPanel.tsx", "sessions/SessionComposer.tsx", "sessions/SessionMessages.tsx", "sessions/SessionMessage.tsx", "sessions/SessionSettings.tsx"].map(path => readFile(new URL(`../src/components/dashboard/${path}`, import.meta.url), "utf8")))).join("\n");
const hook = await readFile(new URL("../src/hooks/useSessionChat.ts", import.meta.url), "utf8");
const history = await readFile(new URL("../src/hooks/useSessionHistory.ts", import.meta.url), "utf8");

test("composer preserves IME composition and Shift+Enter", () => {
  assert.match(panel, /event\.key === "Enter" && !event\.shiftKey && !event\.nativeEvent\.isComposing && event\.nativeEvent\.keyCode !== 229/);
  assert.match(panel, /event\.preventDefault\(\); void chat\.send\(\)/);
});
test("mixed-language messages and prompt render plain text", () => {
  assert.match(panel, /dir="auto" className="session-chat-text">\{view\.body\}/);
  assert.match(panel, /<textarea[^>]+dir="auto"/);
  assert.doesNotMatch(panel, /dangerouslySetInnerHTML|innerHTML/);
});
test("history and send share the persisted session API", () => {
  assert.match(history, /request<HistoryPage>\(`\/api\/sessions\/\$\{sessionId\}\/history\?limit=100`\)/);
  assert.match(hook, /allowFileEgress: fileData.allowFileEgress, fileApprovalScope: fileData.fileApprovalScope/);
  assert.match(hook, /cause\.code === "SessionBusy"/);
  assert.match(hook, /cause\.code === "SessionUnavailable"/);
  assert.match(hook, /setDraft\(""\); await savedHistory.refresh\(\)/);
  assert.match(hook, /Preserve the original failure and the unsent draft; never retry a send/);
});
test("team routing never silently chooses an agent or team", () => {
  assert.doesNotMatch(hook, /effectiveAgentId|dashboard\.agents\.find/);
  assert.match(hook, /cause\.code === "TeamRequired"/);
  assert.match(hook, /cause\.code === "TeamUnavailable"/);
  assert.match(hook, /!teamId && !defaultTeamId/);
  assert.match(hook, /method: "PUT", body: JSON\.stringify\(\{ teamId: clear \? null : teamId \|\| null \}\)/);
  assert.match(panel, /chat\.saveDefaultTeam\(true\)/);
  assert.match(panel, /Save session default/);
});
