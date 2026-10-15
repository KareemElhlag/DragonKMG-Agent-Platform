# DragonKMG implementation report

## 2026-10-15 — foundation sprint

- Created an isolated .NET 9 solution under `dragonkmg/`; no upstream Agent Framework source files changed.
- Implemented SQLite WAL persistence for connections, roles, skills, agents, assignments, and run records.
- Added a loopback-only API and CLI, a generic OpenAI-compatible gateway, environment-only secret references, cloud-egress deny-by-default, and a fail-closed tool policy.
- Added a React dashboard with API-backed creation of connections, roles, skills, agents, and skill assignments.
- Added the dashboard Test Agent workflow; it calls the persisted agent execution endpoint and renders returned run evidence rather than simulating a response.
- Seeded KMG Engineering, Cyber KMG Security, NexusCore UI/UX, and Flutter Architect Review as versioned default instruction skills. Their source references persist in SQLite and they grant no tools.
- Hardened connection validation: HTTPS is required except for explicit loopback endpoints; only `env:NAME` secret references are accepted. Imported skills are disabled until trusted.
- Added an explicit persisted cloud-egress consent action in both CLI and Dashboard; creating a connection never enables cloud routing.
- Protected the management API with a required local shared token. The dashboard sends it only from browser session storage, never from source code or build-time configuration.
- Added startup recovery for interrupted agent runs and durable run-history rendering in the dashboard. Assignment replacement now uses a single SQLite transaction, preventing partially applied skill lists.
- Verified dashboard build, .NET build, unit tests, API health, and dashboard-to-API role/skill assignment persistence. The smoke connection had cloud egress disabled and no provider request was made.

## Known limits

- No live provider was tested: no credentials were provided, and paid calls were not authorized. The OpenAI-compatible response parser is covered by a mocked HTTP test.
- Workflow DAGs, MCP, n8n, approval persistence, and OS vault integration remain incomplete. Interrupted-run recovery and dashboard run history are implemented.
- The Agent Framework package reference compiles under .NET 9; a full runtime adapter remains to be implemented.
- The local API has a single shared local token rather than multi-user authentication/authorization, and must remain loopback-only.
