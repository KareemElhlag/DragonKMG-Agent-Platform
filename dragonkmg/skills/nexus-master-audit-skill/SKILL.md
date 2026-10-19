---
name: nexus-master-audit-skill
description: Evidence-based readiness audit for DragonKMG/NexusCore feature releases, launch decisions, and cross-layer changes. Use after implementation to identify blockers and quantify verified readiness; not a substitute for focused tests or security review.
metadata:
  short-description: Evidence-based NexusCore feature release audit
---

# NexusCore Feature Readiness Audit

Adapted from the user's NexusCore ERP Master Audit skill for the DragonKMG platform. It complements `$kmg-agent-skill` (lifecycle and Senior Monitor), `$nexus-feature-performance` (performance and concurrency), and `$cyber-kmg-security` (security depth). Do not duplicate specialist checklists; collect their evidence and identify what was not reviewed.

## Evidence rule

Every verdict is based on the current tree and checks actually run for this task. Read the requested scope, current diff, affected contracts, and relevant runtime. Never carry forward old test counts, scores, CI badges, or readiness claims. An unrun or unavailable check is `UNVERIFIED` or `BLOCKED`, not a pass.

## Audit lenses

Select lenses by blast radius; explain exclusions:

- **Architecture and contracts:** ownership, layer boundaries, API/event/data schema compatibility, migrations and rollback.
- **Data integrity and tenancy:** tenant-scoped reads/writes, transaction boundaries, invariants, idempotency, event/session lineage and recoverability. For financial/inventory domains, require their applicable ledger/stock invariants rather than generic success responses.
- **Security:** authentication/authorization enforcement, tool grants, secret handling, untrusted input, tenant isolation, integration/webhook boundaries, deployment/container risks. Use `$cyber-kmg-security` when available; do not perform intrusive active tests without explicit scope and authorization.
- **Reliability and runtime:** startup/health of changed services, dependency readiness, timeouts/cancellation, retries, queue limits, observability without sensitive data, and rollback/recovery.
- **Performance and cost:** current baseline/delta for changed hot paths, query/payload bounds, memory/concurrency/event behavior, agent/token usage where available. Route detailed analysis to `$nexus-feature-performance`.
- **User workflow:** loading, error, empty, permission-denied, retry and recovery states; responsive/RTL behavior only where UI changed. Route detailed visual checks to the applicable web or Flutter skill.
- **Verification:** focused unit/integration/contract/e2e checks for changed behavior, exact pass/fail/skip counts, durations if available, and live readback when the claim concerns a running service.

## Decision procedure

1. Define release surface and consequence of failure; inspect current diff and relevant design contracts.
2. Run the narrowest tests that falsify the changed behavior, then necessary integration/runtime checks. Keep raw sensitive output in the approved ignored evidence area; commit summaries, not secrets or raw customer data.
3. Gather specialist findings, reconcile each against current artifacts, and distinguish direct evidence from inference.
4. Provide a readiness percentage only when a task-specific weighted checklist is shown. State denominator, weights, which items are verified/failed/blocked/not applicable, and why. If no defensible weighting exists, give a categorical verdict instead of inventing precision.
5. Issue one verdict: `CERTIFIED`, `CERTIFIED_WITH_RESIDUE`, `REWORK_REQUIRED`, or `BLOCKED`. A failed critical security, data-integrity, compatibility, or recovery control prevents full certification. Missing required evidence prevents a pass; non-critical residue needs an owner and next check.
6. Name the top blockers, accepted residue, evidence checked, checks not run, and one concrete next action. The KMG Senior Monitor remains the independent final certifier and may reject this audit.

## Output shape

- **Verdict:** status and scope (no unsupported marketing claims).
- **Verified:** checks, exact results, commit/runtime where relevant.
- **Blockers:** issue, impact, evidence, owner/next action.
- **Residue / unverified:** check and missing prerequisite.
- **Readiness percentage:** optional; include method and denominator, otherwise omit.
- **Next action:** one highest-value step.

## Boundaries

This is engineering readiness triage, not a compliance attestation, penetration-test authorization, or guarantee of production safety. Do not mutate external systems, deploy, publish, or push based solely on a favorable verdict. No skill text grants tools, permissions, or approval.

## Attribution

This project copy is adapted from the user-provided local `nexus-master-audit-skill` source (owner Karim-(KaReem Elhlag)-Abdelhady). Its evidence-based audit framing was informed by the `production-audit` skill from Everything Claude Code, commit `76aaa77df6adc5b889e689007bf99b9a4f74af34`, Copyright (c) 2026 Affaan Mustafa, MIT License; the required notice is preserved in `references/LICENSE-MIT.md`. The generic Playwright demo bundle was intentionally not copied because it is not a DragonKMG test suite.
