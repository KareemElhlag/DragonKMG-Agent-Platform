# DragonKMG commercial-edition roadmap

## Purpose

This document is the compatibility contract between the current local-first foundation and future paid editions. A paid edition may add capabilities, but it must preserve persisted configuration, never downgrade security controls, and never label a mocked or policy-only feature as a live integration.

## Current foundation — verified locally

- .NET 9 solution, SQLite WAL persistence, loopback management API, and React dashboard.
- Provider connections use environment-variable references only; cloud egress starts disabled.
- Roles, versioned skills, explicit skill assignment/removal, and independent tool grants persist in SQLite.
- Default council: Execution, Quality Review, and Security Test agents, with KMG, Cyber, UI/UX, and Flutter review skills.
- Sequential council workflow with four displayed checklist stages, bounded evidence handoff, and stop-on-failure behavior.
- Agent modes: execution, plan only without code, question only without code, and review only.
- Tool-backed skills fail closed unless every declared tool ID has an explicit grant.
- `workspace.file.read` safely reads permitted text files only from an explicit workspace root.
- Run history, interrupted-run recovery, and privacy-safe local audit events.

## Compatibility invariants for paid editions

1. Existing SQLite data must migrate forward transactionally, with a tested backup before schema changes.
2. `env:` references remain valid. OS-vault references must be additive, never silently rewrite or reveal a secret.
3. Cloud egress remains off after upgrade unless the customer explicitly enables it again under the paid edition's policy.
4. Existing agent, role, skill, assignment, tool-grant, run, and audit identifiers remain stable.
5. A paid entitlement may unlock a feature, but disabled/unlicensed behavior must not delete data or remove the ability to export it.
6. Tool grants and approval requirements must remain fail-closed after migration.
7. Customer data, prompts, and secrets must never be sent to licensing or telemetry services without explicit policy and consent.

## Paid-edition delivery checklist

### Security and identity

- [ ] OS-protected vault adapters: Windows Credential Manager/DPAPI, macOS Keychain, Linux Secret Service, with `env:` fallback.
- [ ] Local user accounts, password/passkey policy, session rotation, and role-based authorization.
- [ ] Tenant/workspace ownership boundaries and tenant-aware audit events.
- [ ] Durable approvals with expiration, cancellation, and reconciliation of side effects.
- [ ] Exportable, tamper-evident audit reports with retention controls.

### Runtime and integrations

- [ ] Microsoft Agent Framework runtime adapter verified against pinned framework version.
- [ ] Workflow DAG editor: conditional routes, fan-out/fan-in, bounded retry, cancellation, and checkpoints.
- [ ] MCP client: trusted-server registry, schema validation, explicit grants, response sanitization, and mock integration tests.
- [ ] n8n connector: host allowlist, vault credential references, signed webhook verification, idempotency key, and retry policy.
- [ ] Model capability catalog, health checks, provider routing policy, token/cost budgets, and rate limits.

### Operations and deployment

- [ ] Hardware-aware queue, concurrency limits, resource refusal, and backpressure.
- [ ] Redacted structured telemetry, diagnostics export with consent, and retention controls.
- [ ] PostgreSQL adapter and migration/restore verification.
- [ ] Encrypted backup/restore, installer, upgrade/rollback, and named Windows/macOS/Linux smoke matrix.
- [ ] Licensing module isolated from runtime and offline-grace behavior with no customer-data loss.
- [ ] GitHub Release pipeline: self-contained archives per supported runtime identifier, checksums, signatures, provenance, and manual-download verification guidance.

## Release gates

- Every checked item needs a command, exit code, redacted evidence, environment, and owner in `TEST_RESULTS.md`.
- Mock tests are `VERIFIED_MOCK`; paid marketing claims require `VERIFIED_LIVE` evidence in a supported environment.
- A release is blocked if it weakens explicit grants, cloud consent, workspace boundaries, secret redaction, or audit retention.
