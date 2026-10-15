# Security Methodology Reference

Use this reference for authorized security planning, penetration-test scoping, active-test safety, evidence handling,
and KMG-style acceptance gates.

## Planning Inputs

Collect only the fields needed for the test:

- Authorization owner and target owner.
- Scope: domains, IP ranges, repos, APIs, mobile apps, containers, CI/CD projects, cloud accounts, and excluded assets.
- Environment: local, staging, production, intentionally vulnerable lab, or isolated copy.
- Allowed tests: code review, SAST, dependency scan, secrets scan, container scan, DAST, manual workflow testing, fuzzing,
  load-sensitive checks, social engineering, physical access, wireless, or out of scope.
- Accounts and roles: admin, warehouse manager, branch cashier, accountant, auditor, API client, background worker.
- Data policy: synthetic data only, masked production data, or approved live data constraints.
- Safety: rate limits, lockout protections, monitoring contacts, rollback plan, and stop conditions.

If these are unknown, run passive checks only and mark active testing as blocked.

## When To Run `run-safe-baseline.ps1`

Run it, not just reference it, at the start of step 1 below, every time this skill is invoked for a task that
touches new or previously-unreviewed source (a new service, a new app, a change that was never carried through a
security pass before) — not only when a dedicated "security review" is explicitly requested. It is passive,
read-only, and cheap, so there is no scope reason to skip it. Concretely:

```powershell
./scripts/run-safe-baseline.ps1 -Root <repo-or-service-root>
```

Record its `SecretMatches`, `DockerRiskMatches`, and the artifact paths in the evidence log even when all three
are zero — a zero count IS the evidence that this pass ran and found nothing, which is different from the pass
never having run at all. A review that skips this and goes straight to manual/targeted checks cannot later claim
"no secrets found" as a blanket statement, only "no secrets found in the specific files manually inspected."
This script is a heuristic first pass, not a full secret-scanning tool — a clean run narrows risk, it does not
close the "secrets" line item outright.

## Safe Execution Order

1. Passive repository review: run `run-safe-baseline.ps1` (see above) plus manual review of auth/permission code,
   tenant filters, logging, config, Dockerfiles, compose files, CI definitions, dependency manifests.
2. Build and analyzer checks: compiler warnings, linters, unit/security tests, package audit.
3. Dependency and supply-chain review: outdated/vulnerable packages, unsigned artifacts, unpinned images, risky scripts.
4. Container and runtime review: exposed ports, Docker socket, root user, privileged mode, secrets in image layers,
   writable filesystems, missing health checks, weak network separation.
5. Authenticated application checks in safe environments: authorization matrix, object-level access, CSRF where relevant,
   CORS, session expiry, password reset, upload/download, reports, imports, exports.
6. Controlled DAST/manual tests only inside the authorized target and rate limits.

## ERP Threat Model Focus

- Tenant breakout: one tenant reads, writes, exports, or infers another tenant's data.
- Branch/warehouse privilege escalation: a branch user operates central warehouse stock or another branch's records.
- Ledger tampering: journals, COGS, opening balances, depreciation, or audit trails are modified without valid authority.
- Money manipulation: raw decimal/double, rounding drift, currency confusion, discount abuse, tax/rate injection.
- Inventory workflow abuse: duplicate transfer posting, replayed approvals, negative stock bypass, stale approval state.
- Reporting leakage: exports expose hidden fields, deleted records, secrets, internal ids, or unauthorized aggregates.
- Background job abuse: replay, idempotency failure, queue poisoning, unexpected tenant context.

## Active Testing Safety

- Use synthetic users, synthetic tenants, and synthetic stock/money values where possible.
- Put unique run ids in test data so cleanup and log correlation are reliable.
- Prefer proof by blocked response, authorization denial, or validation error over proof by harmful state change.
- Never leave accounts, webhooks, files, scheduled jobs, tokens, or feature flags behind after a test.
- Stop immediately on signs of service instability, account lockouts, data corruption, unexpected external traffic, or
  production-only side effects.

## Evidence Quality

A finding is strong when it includes:

- Scope and environment.
- Preconditions and user role.
- Minimal reproducible steps or code location.
- Observed result and expected secure result.
- Business impact for NexusCore/ERP.
- Severity and confidence.
- Remediation and regression test recommendation.
- Retest status.

Scanner output alone is not enough for high severity. Verify reachability, exploitability, and business impact.

## References

- OWASP WSTG: https://wstg.owasp.org/
- OWASP ASVS: https://owasp.github.io/www-project-application-security-verification-standard/
- NIST SP 800-115: https://csrc.nist.gov/pubs/sp/800/115/final
