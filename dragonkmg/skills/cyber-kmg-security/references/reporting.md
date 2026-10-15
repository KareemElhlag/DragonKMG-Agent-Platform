# Security Reporting Reference

Use this reference when writing security findings, remediation plans, run logs, or final security summaries.

## Evidence Directory

Raw logs and intermediate evidence belong in:

```text
security-temp/<yyyyMMdd-HHmmss>-<short-scope>/
```

The directory must be ignored by Git and Docker. Durable reports may live in the project documentation only after
secrets and raw dumps are removed.

## Run Record Shape

```markdown
# Security Run Record

- Run id:
- Date/time:
- Assessor:
- Authorization:
- Scope:
- Environment:
- Exclusions:
- Tools and versions:
- Checks run:
- Checks blocked:
- High-risk findings:
- Residual risk:
- Retest status:
```

## Finding Shape

```markdown
## [SEV] Title

- Status: Open | Fixed | Accepted Risk | False Positive | Not Applicable | Blocked
- Severity: Critical | High | Medium | Low | Informational
- Confidence: High | Medium | Low
- Scope:
- Affected asset:
- Role/preconditions:
- Evidence summary:
- Expected secure behavior:
- Observed behavior:
- Business impact:
- Root cause:
- Remediation:
- Regression test:
- Retest evidence:
```

## Severity Calibration

- Critical: cross-tenant data access, remote code execution, auth bypass, secret exfiltration, ledger/stock/money tampering,
  production compromise, or durable privilege escalation.
- High: unauthorized sensitive operation, serious data exposure, exploitable injection, major workflow bypass, exposed
  admin/runtime surface, or high-confidence exploitable dependency in reachable code.
- Medium: constrained exposure, missing defense-in-depth, limited privilege escalation, important misconfiguration, or
  unverified scanner result with plausible reachability.
- Low: hardening gap, minor information disclosure, weak monitoring, missing non-critical header, or low-impact config.
- Informational: useful context without direct security impact.

## Redaction Rules

Never place these in durable reports:

- Access tokens, refresh tokens, cookies, private keys, connection strings, passwords, recovery codes.
- Full database dumps or production records.
- Full HTTP request/response bodies containing personal, financial, or tenant data.
- Working exploit artifacts or destructive payloads.
- Private infrastructure diagrams beyond the approved audience.

Use summaries and fingerprints instead: token prefix/suffix, hash, count, object id pattern, line reference, or sanitized
request shape.

## KMG Closing Compatibility

For KMG/NexusCore tasks, final security summaries should include:

- **تم إنجازه**: checks completed, scope covered, fixes applied, evidence location.
- **تم تحسينه**: controls strengthened, tests added, monitoring/logging improved.
- **المتبقي**: blocked checks, accepted risk, production approvals needed, highest-value next step.

Do not mark security work complete when active tests were blocked. Say exactly what was verified and what was not.
