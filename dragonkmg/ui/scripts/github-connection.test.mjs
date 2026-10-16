import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const hook = readFileSync(new URL('../src/hooks/useGitHubConnection.ts', import.meta.url), 'utf8');
const panel = readFileSync(new URL('../src/components/dashboard/sessions/GitHubConnectionPanel.tsx', import.meta.url), 'utf8');
test('GitHub credential entry stays ephemeral and is cleared before every attempted connection', () => {
  assert.match(panel, /type="password"/);
  assert.match(panel, /autoComplete="off"/);
  assert.match(panel, /setToken\(""\); if \(value\) void github\.connect\(value\)/);
  assert.doesNotMatch(hook + panel, /localStorage|sessionStorage|console\.|dangerouslySetInnerHTML/);
});
test('GitHub writes require explicit controls and refresh from the persisted API', () => {
  assert.match(hook, /if \(lock\.current \|\| busy\) return/);
  assert.match(hook, /if \(current === generation\.current\) await refresh\(\)/);
  assert.match(hook, /projectId \? mutate\(`/);
  assert.match(panel, /onClick=\{\(\) => void github\.bind\(repository\.fullName\)\}/);
  assert.match(panel, /disabled=\{github\.busy \|\| !projectId/);
});
test('GitHub queries are bounded and stale responses do not overwrite current project', () => {
  assert.match(hook, /AbortSignal\.timeout\(30000\)/);
  assert.match(hook, /Math\.max\(1, value - 1\)/);
  assert.match(hook, /Math\.min\(100, value \+ 1\)/);
  assert.match(hook, /current === generation\.current/);
  assert.match(hook, /generation\.current\+\+/);
  assert.doesNotMatch(panel, /href=\{repository\.htmlUrl\}/);
});
