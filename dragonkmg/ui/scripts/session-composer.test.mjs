import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
const source = await readFile(new URL("../src/components/dashboard/sessions/SessionComposer.tsx", import.meta.url), "utf8");
const css = await readFile(new URL("../src/components/dashboard/sessions/composer.css", import.meta.url), "utf8");
test("composer autogrows from actual content without moving the transcript", () => {
  assert.match(source, /useLayoutEffect/);
  assert.match(source, /element\.style\.height = "0px"/);
  assert.match(source, /Math\.min\(maximum, Math\.max\(minimum, element\.scrollHeight\)\)/);
  assert.match(source, /rows=\{1\}/);
  assert.doesNotMatch(source, /scrollIntoView|scrollTo\(/);
  assert.match(css, /min-height: 64px/);
  assert.match(css, /max-height: 180px/);
  assert.match(css, /max-height: 130px/);
});
test("attachment dismissal restores focus without granting permissions", () => {
  assert.match(source, /attachment\.current\?\.focus\(\)/);
  assert.match(source, /aria-controls="session-file-context"/);
  assert.match(source, /event\.key === "Escape" && filesOpen/);
  assert.match(source, /type="button" disabled title=\{t\("Permissions are enforced/);
  assert.match(source, /aria-describedby="session-composer-hint"/);
  assert.match(source, /!event\.nativeEvent\.isComposing && event\.nativeEvent\.keyCode !== 229/);
});
