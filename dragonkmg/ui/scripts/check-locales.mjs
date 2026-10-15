import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import ts from "typescript";
const root = new URL("../src/", import.meta.url);
const dictionaries = Object.fromEntries(
  ["ar", "en", "es", "de"].map((locale) => [
    locale,
    JSON.parse(
      readFileSync(new URL("locales/" + locale + ".json", root), "utf8"),
    ),
  ]),
);
const keys = Object.keys(dictionaries.en).sort();
for (const [locale, dictionary] of Object.entries(dictionaries)) {
  assert.deepEqual(
    Object.keys(dictionary).sort(),
    keys,
    locale + " key parity",
  );
  for (const [key, value] of Object.entries(dictionary))
    assert.ok(typeof value === "string" && value.trim(), locale + ": " + key);
}
let references = 0;
function visitFile(path) {
  const source = ts.createSourceFile(
    path,
    readFileSync(path, "utf8"),
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TSX,
  );
  function visit(node) {
    if (ts.isJsxAttribute(node) && ['title', 'caption'].includes(node.name.getText(source)) && node.initializer && ts.isStringLiteral(node.initializer)) {
      assert.ok(Object.hasOwn(dictionaries.en, node.initializer.text), path + ': missing panel text ' + node.initializer.text)
      references++
    }
    if (
      ts.isCallExpression(node) &&
      node.expression.getText(source) === "t" &&
      ts.isStringLiteral(node.arguments[0])
    ) {
      assert.ok(
        Object.hasOwn(dictionaries.en, node.arguments[0].text),
        path + ": missing " + node.arguments[0].text,
      );
      references++;
    }
    ts.forEachChild(node, visit);
  }
  visit(source);
}
function walk(path) {
  for (const entry of readdirSync(path, { withFileTypes: true })) {
    const target = join(path, entry.name);
    if (entry.isDirectory()) walk(target);
    else if (/\.tsx?$/.test(entry.name)) visitFile(target);
  }
}
walk(fileURLToPath(root));
console.log(
  "PASS: four locales, " +
    keys.length +
    " keys each; " +
    references +
    " literal translation references checked",
);
