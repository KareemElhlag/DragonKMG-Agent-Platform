import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const hook = readFileSync(new URL("../src/hooks/useTeams.ts", import.meta.url), "utf8");
const panel = readFileSync(new URL("../src/components/dashboard/TeamsPanel.tsx", import.meta.url), "utf8");

test("team provisioning reads the production template and sends explicit model bindings", () => {
  assert.match(hook, /request<TeamTemplate\[\] \| TeamTemplate>\("\/api\/teams\/templates"\)/);
  assert.match(hook, /request<\{ team: TeamDefinition; agents: unknown\[\] \}>\("\/api\/teams\/provision"/);
  assert.match(hook, /JSON\.stringify\(\{ templateKey, connectionId, modelId \}\)/);
  assert.match(panel, /setProvisionedTeam\(result\.team\)/);
  assert.match(panel, /Promise\.all\(\[data\.refresh\(\), dashboard\.load\(\)\]\)/);
});

test("provisioning UI shows ordered template agents, RTL-safe identifiers, and recoverable states", () => {
  assert.match(panel, /selectedTemplate\.agents\.map/);
  assert.match(panel, /<bdi>/);
  assert.match(panel, /data\.templatesLoading/);
  assert.match(panel, /data\.refreshTemplates\(\)/);
  assert.match(panel, /provisionError/);
  assert.match(panel, /provisionedTeam/);
  assert.match(panel, /disabled=\{provisionBusy \|\| !connectionId \|\| !modelId/);
});
