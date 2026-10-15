# DragonKMG feature matrix

| Feature | Status | Evidence / limitation |
| --- | --- | --- |
| .NET 9 bootstrap | VERIFIED_MOCK | `dotnet build DragonKMG.sln` completed with 0 warnings and 0 errors. |
| SQLite persistence | VERIFIED_MOCK | API smoke created and read roles, skills, connections, agents, and assignments. |
| Provider connections | CODED | Generic OpenAI-compatible endpoint and environment secret reference; no live credential test. |
| Cloud policy | VERIFIED_MOCK | Cloud connection starts with `CloudEgressAllowed=false`; application rejects cloud execution. |
| Roles and skills | VERIFIED_MOCK | Dashboard/API persistence and effective-instruction composition smoke-tested. |
| Agent skill authorization | PARTIAL | Tool policy denies ungranted and unsafe execution; granular persisted tool grants are not yet implemented. |
| Agent Framework adapter | CODED | Compiles against Microsoft.Agents.AI 1.23.0; runtime execution adapter remains incomplete. |
| CLI and loopback API | VERIFIED_MOCK | CLI compiles; API health checked on 127.0.0.1. |
| Dashboard | VERIFIED_MOCK | Vite production build passed; browser interaction not yet inspected. |
| MCP, n8n, workflows, recovery | NOT_STARTED | Deferred after P0 foundation. |
