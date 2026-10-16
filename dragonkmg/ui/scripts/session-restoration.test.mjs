import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import ts from 'typescript';

const file = name => readFileSync(new URL(`../src/${name}`, import.meta.url), 'utf8');
const compiled = ts.transpileModule(file('services/sessionSelection.ts'), { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
const { readSessionSelection } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString('base64')}`);
const saved = '6fed5d74-6990-4536-8186-115500d1c6c5';
const other = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

test('opening the workspace restores a remembered session, not a random session', () => {
  assert.equal(readSessionSelection('http://localhost/', saved), saved);
  assert.equal(readSessionSelection('http://localhost/', null), '');
});
test('explicit URL session wins; invalid explicit selection never silently falls back', () => {
  assert.equal(readSessionSelection(`http://localhost/?session=${other}`, saved), other);
  assert.equal(readSessionSelection('http://localhost/?session=invalid', saved), '');
});
test('history loading is independent of project and team configuration', () => {
  const history = file('hooks/useSessionHistory.ts');
  assert.match(history, /\/history\?limit=100/);
  assert.doesNotMatch(history, /\/team|\/project|Promise.all/);
  assert.match(history, /before=\$\{encodeURIComponent\(state.nextCursor\)\}/);
  assert.match(history, /generation.current/);
  assert.match(history, /existing.has\(message.id\)/);
});
test('message content and drafts are not added to persistent browser storage', () => {
  assert.doesNotMatch(file('hooks/useSessionChat.ts'), /localStorage.setItem|sessionStorage.setItem/);
  assert.match(file('services/sessionSelection.ts'), /localStorage.setItem\(key, value\)/);
});
test('project mutations and late completions cannot replace a new session binding', () => {
  assert.match(file('hooks/useSessionChat.ts'), /if \(lock.current \|\| projectData.projectBusy\) \{ rememberSession\(sessionId\)/);
  assert.match(file('hooks/useSessionProjects.ts'), /if \(current !== generation.current\) return/);
});
test('changing sessions resets bottom-following and permission placeholder cannot grant access', () => {
  assert.match(file('hooks/useTranscriptScroll.ts'), /if \(session.current !== sessionId\)/);
  assert.match(file('hooks/useTranscriptScroll.ts'), /following.current = true/);
  const composer = file('components/dashboard/sessions/SessionComposer.tsx');
  assert.match(composer, /disabled title=\{t\("Permissions are enforced by the engine/);
  assert.doesNotMatch(composer, /type="file"|FormData|\/grants|\/tools/);
});
