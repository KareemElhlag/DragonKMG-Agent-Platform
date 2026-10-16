import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const file = name => readFileSync(new URL(`../src/${name}`, import.meta.url), 'utf8');

test('file egress consent is bound to session project team and exact draft', () => {
  const hook = file('hooks/useFileContext.ts');
  assert.match(hook, /scope = `\$\{sessionId\}:\$\{projectId\}:\$\{teamId \|\| defaultTeamId\}:\$\{teamDirective\}`/);
  assert.match(hook, /approval.scope === scope && approval.draft === draft && approval.allowed/);
  assert.match(hook, /allowed: false/);
  assert.match(file('hooks/useSessionChat.ts'), /fileData.resetFileConsent\(\)/);
});
test('preview is local endpoint and stale path preview cannot add a reference', () => {
  const hook = file('hooks/useFileContext.ts');
  assert.match(hook, /\/files\/preview/);
  assert.match(hook, /preview\?\.scope === scope && preview.path === filePath/);
  assert.match(hook, /if \(!filePreview \|\| busy \|\| fileBusy/);
  assert.doesNotMatch(hook, /\/tools|FormData|type="file"/);
  assert.match(hook, /setDraft\(\[lines\[0\], directive, \.\.\.lines.slice\(1\)\]/);
});
