# DragonKMG feature matrix

| Feature | Status | Evidence / limitation |
| --- | --- | --- |
| .NET 9 bootstrap | VERIFIED_MOCK | `dotnet build DragonKMG.sln` completed with 0 warnings and 0 errors. |
| SQLite persistence | VERIFIED_MOCK | API smoke created and read roles, skills, connections, agents, and assignments. |
| Provider connections | VERIFIED_MOCK | Generic OpenAI-compatible endpoint has a mocked response-parser test; no live credential test. |
| Cloud policy | VERIFIED_MOCK | Cloud connection starts with `CloudEgressAllowed=false`; explicit CLI/Dashboard action persists the approval before execution. |
| Roles and skills | VERIFIED_MOCK | Dashboard/API persistence and effective-instruction composition smoke-tested. Four KMG-family skills are seeded by default with explicit sources. |
| Agent skill authorization | PARTIAL | Tool policy denies ungranted and unsafe execution; granular persisted tool grants are not yet implemented. |
| Agent Framework adapter | CODED | Compiles against Microsoft.Agents.AI 1.23.0; runtime execution adapter remains incomplete. |
| CLI and authenticated loopback API | PARTIAL | API is loopback by default and requires a shared local token for `/api/*`; an authenticated endpoint smoke test is pending final verification. |
| Dashboard | VERIFIED_MOCK | Vite production build passed; it persists configuration/assignments and invokes the real Test Agent endpoint. Browser interaction not yet inspected. |
| Run evidence and interruption recovery | VERIFIED_MOCK | Runs persist in SQLite; API startup marks incomplete `Running` runs as `Failed` with `InterruptedByRestart`, covered by a focused test and exposed in the dashboard history. |
| Defensive foundation review | VERIFIED_MOCK | Passive Cyber KMG baseline ran: 1 reviewed false positive, 0 Docker-risk matches. |
| MCP, n8n, workflow DAGs | NOT_STARTED | Deferred after P0 foundation; interruption recovery is implemented separately. |
