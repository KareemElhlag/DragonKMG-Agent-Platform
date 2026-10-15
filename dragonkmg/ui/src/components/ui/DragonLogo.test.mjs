import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

test("brand has one SVG source across the component and favicon", async () => {
  const component = await readFile(new URL("./DragonLogo.tsx", import.meta.url), "utf8");
  const html = await readFile(new URL("../../../index.html", import.meta.url), "utf8");
  const svg = await readFile(new URL("../../../public/dragon-mark.svg", import.meta.url), "utf8");
  assert.match(component, /src="\/dragon-mark.svg"/);
  assert.match(html, /href="\/dragon-mark.svg"/);
  assert.match(svg, /viewBox="0 0 64 64"/);
  assert.doesNotMatch(svg, /<script|<image|https?:\/\/(?!www\.w3\.org)/);
});

test("decorative marks do not duplicate the nearby wordmark", async () => {
  const component = await readFile(new URL("./DragonLogo.tsx", import.meta.url), "utf8");
  assert.match(component, /decorative = true/);
  assert.match(component, /alt=\{decorative \? "" : "DragonKMG"\}/);
  assert.match(component, /width=\{size\}/);
  assert.match(component, /height=\{size\}/);
});
