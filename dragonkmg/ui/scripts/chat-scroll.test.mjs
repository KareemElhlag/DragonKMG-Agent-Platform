import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("../src/services/chatScroll.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText;
const { isNearLatest, restoredScrollTop } = await import(`data:text/javascript;base64,${Buffer.from(compiled).toString("base64")}`);

test("follow threshold tolerates subpixels and short histories", () => {
  assert.equal(isNearLatest({ top: 121, height: 600, viewport: 400 }), true);
  assert.equal(isNearLatest({ top: 119, height: 600, viewport: 400 }), false);
  assert.equal(isNearLatest({ top: 0, height: 200, viewport: 400 }), true);
});
test("pinned reader follows content growth and viewport shrink", () => {
  assert.equal(restoredScrollTop({ top: 200, height: 1200, viewport: 400 }, true), 800);
  assert.equal(restoredScrollTop({ top: 800, height: 1200, viewport: 300 }, true), 900);
});
test("history reader retains visual anchor when older messages prepend or details above expand", () => {
  assert.equal(restoredScrollTop({ top: 200, height: 1800, viewport: 400 }, false, 600), 800);
  assert.equal(restoredScrollTop({ top: 800, height: 2000, viewport: 400 }, false, 200), 1000);
  assert.equal(restoredScrollTop({ top: 1000, height: 1800, viewport: 400 }, false, -200), 800);
});
test("new answer or process row below history does not drag the reader", () => {
  assert.equal(restoredScrollTop({ top: 200, height: 2400, viewport: 400 }, false), 200);
});
test("collapsed content and short transcripts clamp within scroll bounds", () => {
  assert.equal(restoredScrollTop({ top: 800, height: 500, viewport: 400 }, false, -900), 0);
  assert.equal(restoredScrollTop({ top: 800, height: 500, viewport: 400 }, false), 100);
  assert.equal(restoredScrollTop({ top: 0, height: 200, viewport: 400 }, true), 0);
});
