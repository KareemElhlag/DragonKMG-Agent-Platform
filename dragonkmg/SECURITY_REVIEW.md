# Security review — local foundation

- Date: 2026-10-15
- Scope: `dragonkmg/` local source and loopback API only.
- Authorization: repository owner requested defensive review and hardening.
- Environment: local development; no external hosts, credentials, live providers, or active probing.
- Checks: Cyber KMG safe baseline, code/configuration review, .NET tests, and production UI build.

## Results

- Secret-pattern baseline: 2 heuristic matches, both reviewed as false positives: the private SQLite connection-string variable name and a test-only secret fixture. No secret value is stored in production configuration.
- Docker-risk baseline: 0 matches.
- Dependency manifests inventoried: 13. Dependency vulnerability scans were not run because no project-specific scanner is configured yet.
- API binds to `127.0.0.1` by default. Provider endpoints require HTTPS unless they are explicit loopback HTTP endpoints.
- Provider secret references support `env:NAME` and `vault:NAME`; plaintext values are not persisted. On Windows, vault values are protected with DPAPI for the current user.
- Cloud execution is denied unless the connection explicitly allows egress.
- Imported skills are stored disabled and untrusted by default. Default skill assignments carry no tool grants.
- Process execution remains denied unless a sandbox is both available and explicitly granted.
- Every `/api/*` request requires `DRAGONKMG_LOCAL_API_TOKEN` and a matching `X-DragonKMG-Local-Token` header. The comparison is fixed-time, and the dashboard keeps the value only in browser session storage.
- Incomplete runs are finalized on process restart, retaining a durable error code rather than appearing indefinitely active.

## Residual risk

- The API uses one local shared token, not per-user identity or authorization. Durable audit trails and integration endpoint allowlists are implemented. Per-user identity, rate limits, CSRF protection, and cross-platform OS vault adapters are not yet implemented; do not expose the API beyond loopback.
- No live provider, MCP, n8n, Docker, or browser security test was run.
- No active security tests were performed; they are blocked pending an explicit target, accounts, rate limit, and test window.
