# Senior Monitor Certification Rubric

## Evidence bundle

- Original request and accepted scope.
- Classification, architecture map, and changed-file list.
- Role map and multi-agent council outputs when agents were used.
- Focused and contract test results tied to the current tree.
- Runtime/container readback where visibility matters.
- Security, tenant, money/inventory, network, and job checks when relevant.
- UX/mobile/Flutter evidence when visible workflows or Flutter/Dart surfaces changed.
- Rollback path and migration safety.

## Gates

| Area | Pass condition |
|---|---|
| Scope | Every changed path maps to an accepted requirement. |
| Architecture | One clear owner and source of truth; no accidental coupling. |
| Correctness | Critical invariants have executable tests or an explicit blocker. |
| Security | Authorization, tenant boundaries, secrets, and validation fail closed. |
| Mobile/Flutter | State, offline/sync behavior, secure storage/logging, and platform assumptions are verified when relevant. |
| Reliability | Timeouts, retries, idempotency, and jobs are defined. |
| Performance | Baseline and delta exist, or the gap is documented. |
| Runtime | The serving artifact is proven to contain the change. |
| UX | Loading, empty, error, permission, RTL, mobile, and destructive states are covered when relevant. |
| Multi-Agent | Agent outputs are integrated, conflicts resolved by evidence, and no agent certified its own work. |
| Operations | Correlation, metrics, rollback, and residue ownership are present. |

## Decisions

- `CERTIFIED`: applicable gates pass with no material residue.
- `CERTIFIED_WITH_RESIDUE`: safe to proceed with an owned, dated follow-up.
- `REWORK_REQUIRED`: plan or implementation needs correction.
- `BLOCKED`: required evidence or environment is unavailable.
