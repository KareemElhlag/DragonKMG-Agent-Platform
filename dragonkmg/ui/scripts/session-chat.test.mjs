import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const panel = await readFile(new URL("../src/components/dashboard/SessionsPanel.tsx", import.meta.url), "utf8");
const hook = await readFile(new URL("../src/hooks/useSessionChat.ts", import.meta.url), "utf8");

test("composer preserves IME composition and Shift+Enter", () => {
  assert.match(panel, /event\.key === "Enter" && !event\.shiftKey && !event\.nativeEvent\.isComposing && event\.nativeEvent\.keyCode !== 229/);
  assert.match(panel, /event\.preventDefault\(\); void chat\.send\(\)/);
});
test("mixed-language messages and prompt render plain text", () => {
  assert.match(panel, /dir="auto" className="session-chat-text">\{message\.content\}/);
  assert.match(panel, /<textarea[^>]+dir="auto"/);
  assert.doesNotMatch(panel, /dangerouslySetInnerHTML|innerHTML/);
});
test("history and send share the persisted session API", () => {
  assert.match(hook, /request<SessionMessage\[\]>\(`\/api\/sessions\/\$\{sessionId\}\/messages`\)/);
  assert.match(hook, /method: "POST", signal: pending\.signal, body: JSON\.stringify\(\{ agentId: effectiveAgentId, input: draft \}\)/);
  assert.match(hook, /cause\.code === "SessionBusy"/);
  assert.match(hook, /cause\.code === "SessionUnavailable"/);
  assert.match(hook, /setDraft\(""\); await refresh\(\)/);
  assert.match(hook, /Preserve the original failure and the unsent draft; never retry a send/);
});
