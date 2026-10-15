# Cyber KMG Security

Authorized defensive security review, penetration-test planning, hardening, and evidence logging for KMG/NexusCore-style ERP systems.

## Install Locally

Copy or clone this folder into your Codex skills directory:

```powershell
C:\Users\hp\.codex\skills\cyber-kmg-security
```

Then invoke it in Codex with:

```text
$cyber-kmg-security
```

## Use Inside A Project

For passive review or planning:

```text
Use $cyber-kmg-security to create a security test plan for this API.
```

For NexusCore production-grade work, combine it with KMG:

```text
Use $kmg-agent-skill and $cyber-kmg-security to run an authorized defensive security review.
```

For Flutter/mobile security work, combine it with the Flutter skill:

```text
Use $flutter-architect-review and $cyber-kmg-security to review this Flutter app security posture.
```

## Helper Scripts

- `scripts/init-security-run.ps1`: creates `security-temp/<run-id>/` and adds it to `.gitignore` and `.dockerignore`.
- `scripts/security-inventory.ps1`: inventories security-relevant files.
- `scripts/run-safe-baseline.ps1`: performs a safe first-pass scan for potential secrets, Docker risk patterns, and dependency manifests.

## Safety Boundary

This skill is for systems you own or are explicitly authorized to test. It is designed for defensive assessment, evidence, triage, and hardening.

## Main Files

- `SKILL.md`: skill entry point.
- `references/methodology.md`: authorization, scoping, and safe execution.
- `references/checklists.md`: web/API/mobile/container/CI coverage.
- `references/reporting.md`: security finding and run-record format.
