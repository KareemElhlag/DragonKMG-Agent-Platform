# DragonKMG feature matrix

| Feature | Status | Evidence / limitation |
| --- | --- | --- |
| .NET 9 bootstrap | VERIFIED_MOCK | `dotnet build DragonKMG.sln` completed with 0 warnings and 0 errors. |
| SQLite persistence | VERIFIED_MOCK | API smoke created and read roles, skills, connections, agents, and assignments. |
| Provider connections | VERIFIED_MOCK | Generic OpenAI-compatible endpoint has a mocked response-parser test; no live credential test. |
| Cloud policy | VERIFIED_MOCK | Cloud connection starts with `CloudEgressAllowed=false`; explicit CLI/Dashboard action persists the approval before execution. |
| Roles and skills | VERIFIED_MOCK | Dashboard/API persistence and effective-instruction composition smoke-tested. Four KMG-family skills are seeded by default with explicit sources. |
| Agent operating modes | VERIFIED_MOCK | Dashboard and SQLite persist Execution, Plan-only, Question-only, and Review modes. Runtime inserts mode-specific no-code/no-execution guardrails for plan, question, and review; focused test covers the plan guardrail. |
| Default three-agent council | VERIFIED_MOCK | Explicit provisioning creates Execution, Quality Review, and Security Test agents with the four local default skills. Re-running does not overwrite user-customized agents or assignments; focused test covers creation. |
| Sequential council workflow | VERIFIED_MOCK | The API and dashboard execute Execution → Quality Review → Security Test Review in order. Each stage receives bounded prior evidence and later stages do not start after a failure; mocked focused test covers the handoff. |
| Workflow stages and checklist | VERIFIED_MOCK | The default council workflow exposes four API-backed stages, owners, checklists, and expected evidence. The runtime injects the applicable checklist and explicit no-invention rule into every stage. |
| Agent skill authorization | PARTIAL | Tool policy and persisted grants fail closed before agent execution. Actual MCP/n8n/host-tool invocation and approval execution remain incomplete. |
| Persisted agent tool grants | VERIFIED_MOCK | API, SQLite, and dashboard persist explicit per-agent grants. Tool-backed skills are blocked during effective-configuration preflight unless all required IDs are granted; focused test verifies deny then allow behavior. |
| Restricted workspace file reader | VERIFIED_MOCK | `workspace.file.read` is API-accessible only with an explicit grant and configured workspace root. It blocks path escapes, reparse points, binary files, disallowed extensions, and files above 1 MB; focused boundary test passes. |
| Agent Framework adapter | CODED | Compiles against Microsoft.Agents.AI 1.23.0; runtime execution adapter remains incomplete. |
| CLI and authenticated loopback API | PARTIAL | API is loopback by default and requires a shared local token for `/api/*`; an authenticated endpoint smoke test is pending final verification. |
| Cross-platform terminal control plane | VERIFIED_MOCK | `dragonkmg tui` provides an interactive standard-console UI for selecting agents, changing model/mode, viewing policy state, tracking opened workspace files, and submitting prompts. Build and unit suite pass; interactive terminal usability needs named-device smoke coverage. |
| Dashboard | VERIFIED_MOCK | Vite production build passed; it persists configuration/assignments and invokes the real Test Agent endpoint. Browser interaction not yet inspected. |
| Run evidence and interruption recovery | VERIFIED_MOCK | Runs persist in SQLite; API startup marks incomplete `Running` runs as `Failed` with `InterruptedByRestart`, covered by a focused test and exposed in the dashboard history. |
| Defensive foundation review | VERIFIED_MOCK | Passive Cyber KMG baseline ran: 1 reviewed false positive, 0 Docker-risk matches. |
| Windows OS-protected vault | VERIFIED_MOCK | `vault:NAME` resolves through a Windows DPAPI current-user vault, while SQLite stores only the reference. CLI accepts a hidden interactive secret; environment references remain portable fallback. Windows round-trip test passes. |
| Local audit trail | VERIFIED_MOCK | Mutating management API calls persist timestamp, route, status, outcome, and a one-way token-derived actor identifier. Bodies and secrets are excluded; SQLite persistence is covered by a focused test. |
| MCP, n8n, workflow DAGs | NOT_STARTED | Deferred after P0 foundation; interruption recovery is implemented separately. |
