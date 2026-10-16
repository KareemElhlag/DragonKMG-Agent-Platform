import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const source = readFileSync(new URL('../src/components/dashboard/sessions/SessionProjectBar.tsx', import.meta.url), 'utf8');
test('compact project and plugin triggers use native dismissible popovers', () => {
  for (const id of ['chat-project-menu', 'chat-plugin-menu']) {
    assert.ok(source.includes(`popoverTarget="${id}"`));
    assert.ok(source.includes(`id="${id}" className="chat-context-popover" popover="auto" role="dialog"`));
  }
  assert.ok(source.includes('popoverTargetAction="hide"'));
});
test('floating project controls retain real persistence and planning-only plugins', () => {
  assert.ok(source.indexOf('id="chat-project-menu"') < source.indexOf('chat.setProject('));
  assert.ok(source.includes('await chat.createProject('));
  assert.ok(source.includes('savedProject.id === chat.projectId'));
  assert.ok(source.includes('Planning only. No accounts connected or permissions granted.'));
});
