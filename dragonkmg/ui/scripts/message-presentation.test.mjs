import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import ts from 'typescript';
const code = ts.transpileModule(readFileSync(new URL('../src/services/messagePresentation.ts', import.meta.url), 'utf8'), { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
const { presentMessage } = await import(`data:text/javascript;base64,${Buffer.from(code).toString('base64')}`);
const id = '133aa3a2-0b2c-4ae6-b08f-b80b00143ac0';
test('machine team records fold without altering their stored source', () => {
  const content = `Team: Planning (${id}); version: 2; ordered members: ${id}`;
  const value = presentMessage({ role: 'system', content });
  assert.equal(value.kind, 'execution'); assert.equal(value.label, 'Planning'); assert.equal(value.body, '');
  assert.equal(content.includes(id), true);
});
test('generated agent envelope hides technical ID but retains response body IDs', () => {
  const value = presentMessage({ role: 'assistant', agentRunId: id, content: `## Planning (${id})\nReview record ${id}\n## Reviewer (${id})\nLooks good` });
  assert.equal(value.label, 'Planning'); assert.equal(value.body, `Review record ${id}\n## Reviewer (${id})\nLooks good`); assert.equal(value.hasDetails, true);
});
test('user content and ordinary assistant UUIDs are never globally redacted', () => {
  const content = `## Sample (${id})\nKeep ${id}`;
  assert.equal(presentMessage({ role: 'user', content }).body, content);
  assert.equal(presentMessage({ role: 'assistant', content }).body, content);
});
test('operational failure messages stay visible, not folded as metadata', () => {
  const value = presentMessage({ role: 'system', content: 'Team request failed; remaining members were not run.' });
  assert.equal(value.kind, 'message'); assert.match(value.body, /failed/);
});
test('fenced code and markdown examples in the actual answer remain byte-for-byte intact', () => {
  const body = `\`\`\`md\n## Example (${id})\n\`\`\``;
  const value = presentMessage({ role: 'assistant', agentRunId: id, content: `## Planning (${id})\n${body}` });
  assert.equal(value.body, body);
});

test('legacy envelope removal preserves leading blank lines and code indentation', () => {
  const body = '\n    const example = true;\n';
  assert.equal(presentMessage({ role: 'assistant', agentRunId: id, content: `## Planning (${id})\n${body}` }).body, body);
});
