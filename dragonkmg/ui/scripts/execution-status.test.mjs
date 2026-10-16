import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const read = path => readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
test('execution polling correlates requests and has bounded non-overlapping reads', () => {
  const hook = read('hooks/useSessionExecution.ts');
  assert.match(hook, /value\?\.requestId === active/);
  assert.match(hook, /AbortSignal.timeout\(3000\)/);
  assert.match(hook, /setTimeout\(\(\) => void poll\(\), 800\)/);
  assert.doesNotMatch(hook, /setInterval|method: "POST"/);
});
test('status finalization cannot hold the composer send lock', () => {
  const chat = read('hooks/useSessionChat.ts');
  assert.match(chat, /lock.current = false; setBusy\(false\); setSending\(false\);.*void execution.finish\(requestId\)/);
  assert.doesNotMatch(chat, /await execution.finish/);
});
