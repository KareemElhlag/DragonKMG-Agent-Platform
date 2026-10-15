---
name: cyber-kmg-security
description: Run authorized defensive security reviews, penetration-test planning, vulnerability triage, and hardening for KMG/NexusCore-style ERP systems with evidence logs isolated from Git and Docker build contexts.
metadata:
  short-description: Authorized security review, pentest planning, and hardening gates
---

# Cyber KMG Security

Use this skill for authorized security assessment, defensive penetration-test planning, secure-code review,
vulnerability triage, hardening, and evidence registration for applications, APIs, containers, CI/CD, and ERP workflows.
It is built to complement `$kmg-agent-skill`: KMG owns lifecycle, fail-closed evidence, runtime synchronization,
and ERP integrity; this skill owns security scope, attack-surface mapping, safe test selection, severity, and mitigation.

This skill does not promise that 99.9% of all possible vulnerabilities are eliminated. It drives toward measurable,
layered risk reduction by covering the known attack classes, verifying controls against evidence, and recording every
gap as passed, failed, blocked, or not applicable.

## Authorization Boundary

- Work only on systems the user owns or is explicitly authorized to test.
- Prefer local, staging, test, or intentionally vulnerable environments for active testing.
- Do not provide stealth, persistence, credential theft, destructive payloads, malware behavior, or instructions for
  evading detection.
- Active probing must be rate-limited, reversible, and scoped to named hosts, ports, tenants, accounts, and time windows.
- If authorization, target ownership, or blast radius is unclear, stop and ask for the missing scope before active testing.
- Production tests require an explicit safe plan, rollback path, monitoring, and user approval immediately before running.

## Workflow

1. Define scope: assets, environments, URLs, APIs, repos, containers, cloud resources, user roles, data sensitivity,
   allowed test types, excluded targets, rate limits, and time window.
2. Initialize evidence: use `scripts/init-security-run.ps1` or create an equivalent `security-temp/<run-id>/` directory.
   Raw logs stay there and must be ignored by Git and Docker; durable reports contain summaries, commands, counts, and
   decisions, not secrets or full dumps.
3. Map attack surface: entry points, trust boundaries, authentication paths, tenant/branch/warehouse boundaries, file
   upload/import/export flows, background jobs, integrations, CI/CD, secrets, and container/runtime exposure.
4. Select checks using the relevant reference:
   - Read [references/methodology.md](references/methodology.md) for planning, evidence, and safe execution.
   - Read [references/checklists.md](references/checklists.md) for web/API/mobile/container/CI security coverage.
   - Read [references/reporting.md](references/reporting.md) before writing findings, risk decisions, or remediation logs.
5. Run least-risk checks first: SAST, dependency scan, secrets scan, configuration review, container scan, then controlled
   DAST or manual verification only when scope allows.
6. Triage findings by exploitability, business impact, tenant/data exposure, financial/ledger impact, compensating controls,
   and evidence quality.
7. Remediate fail-closed: fix root causes, add regression tests/security controls, rerun focused checks, and record residue.

## Security Priorities For NexusCore ERP

- Tenant isolation, branch/warehouse authorization, object-level access control, and privilege escalation.
- Money, ledger, stock movement, inventory transfer, journal, COGS, opening balance, and depreciation integrity.
- Authentication, session lifecycle, MFA, refresh tokens, password reset, and identity provider integration.
- Input validation, injection prevention, file import/export, report generation, SSRF, path traversal, and deserialization.
- Secrets management, encryption, audit logging, monitoring, alerting, backup/restore, and incident evidence.
- Docker, compose, CI/CD, supply chain, dependency pinning, image scanning, and runtime exposure.

## Tooling Guidance

Use project-approved tools first. Common defensive tools include dependency scanners, secret scanners, SAST analyzers,
container scanners, linters, test suites, and OWASP ZAP-style DAST against authorized environments. The helper
`scripts/run-safe-baseline.ps1` can collect a safe first-pass inventory, potential secret patterns, and Docker risk
patterns without active exploitation. Record exact versions where available. A scanner result is a lead, not proof:
verify impact before labeling a finding critical.

Do not run broad network scans, brute force, fuzzing, high-volume DAST, exploit modules, or destructive payloads unless
the user has explicitly authorized the exact target and test window.

## Evidence And Logs

- Raw security evidence goes in `security-temp/<run-id>/`.
- Ensure `security-temp/` is in `.gitignore` and `.dockerignore` before writing raw logs.
- Never commit secrets, tokens, cookies, full HTTP dumps, private keys, database exports, or exploit artifacts.
- Summaries should include command/tool name, target scope, timestamp, decisive counts, finding ids, and residual risk.
- Missing evidence is `UNVERIFIED`, not passed.

## Source Baseline

Use primary sources for current security baselines: OWASP WSTG, OWASP ASVS, OWASP MASVS/MASTG, OWASP Cheat Sheet Series,
CIS Docker Benchmark, and NIST SP 800-115. When the user asks for the latest security coverage, verify against official
sources before making version-specific claims.
