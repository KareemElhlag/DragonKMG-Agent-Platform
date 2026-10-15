import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = path => readFileSync(new URL(`../src/${path}`, import.meta.url), 'utf8');
test('stale unauthorized responses cannot invalidate replacement credentials', () => {
  assert.match(source('services/api.ts'), /response.status === 401 && token === sessionStorage.getItem\("dragonkmg.localApiToken"\)/);
});
test('authentication gate replaces protected sections and duplicate errors', () => {
  const app = source('App.tsx');
  assert.match(app, /d.error && !d.authRequired/);
  assert.match(app, /d.authRequired \? <section className="auth-recovery"/);
});
test('credential editing uses a masked explicit-submit form', () => {
  const form = source('components/dashboard/AccessSettings.tsx');
  assert.match(form, /type="password"/);
  assert.match(form, /<form onSubmit=/);
  assert.match(form, /onChange=\{\(e\) => setDraft\(e.target.value\)\}/);
  assert.doesNotMatch(form, /localStorage/);
});
