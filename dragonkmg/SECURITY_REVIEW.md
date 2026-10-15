# Security review — local foundation

- Date: 2026-10-15
- Scope: `dragonkmg/` local source and loopback API only.
- Authorization: repository owner requested defensive review and hardening.
- Environment: local development; no external hosts, credentials, live providers, or active probing.
- Checks: Cyber KMG safe baseline, code/configuration review, .NET tests, and production UI build.

## Results

- Secret-pattern baseline: 1 heuristic match, reviewed as a false positive. It is the private SQLite connection-string variable name; no secret value is stored.
- Docker-risk baseline: 0 matches.
- Dependency manifests inventoried: 13. Dependency vulnerability scans were not run because no project-specific scanner is configured yet.
- API binds to `127.0.0.1` by default. Provider endpoints require HTTPS unless they are explicit loopback HTTP endpoints.
- Provider secret references are limited to `env:NAME`; plaintext values are not persisted.
- Cloud execution is denied unless the connection explicitly allows egress.
- Imported skills are stored disabled and untrusted by default. Default skill assignments carry no tool grants.
- Process execution remains denied unless a sandbox is both available and explicitly granted.

## Residual risk

- Local API authentication, per-user authorization, durable audit trails, endpoint allowlists, and CSRF/session protection are not yet implemented; do not expose the API beyond loopback.
- No live provider, MCP, n8n, Docker, or browser security test was run.
- No active security tests were performed; they are blocked pending an explicit target, accounts, rate limit, and test window.
