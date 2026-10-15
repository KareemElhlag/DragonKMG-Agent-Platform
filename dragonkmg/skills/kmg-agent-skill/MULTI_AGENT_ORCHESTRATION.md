<!-- Created By: Karim-(KaReem Elhlag)-Abdelhady -->

# Multi-Agent Orchestration Protocol

Use this protocol when a task benefits from independent agents applying, reviewing, securing, improving UX, and finally
certifying the work. The goal is better evidence, not more motion.

## When to run the council

Use multi-agent execution when at least one is true:

- The task crosses backend, frontend/mobile, security, data, and UX boundaries.
- The change has financial, inventory, tenant-isolation, authentication, authorization, or production-runtime risk.
- Independent review can happen in parallel without blocking the implementer.
- The user explicitly asks for multi-agent work.

Do not use it for tiny edits, isolated documentation changes, one-file typos, or tasks where delegation would add more
risk than signal.

## Default council

| Agent | Purpose | Allowed write scope |
|---|---|---|
| Implementer | Applies the accepted plan and focused tests. | Primary implementation files only. |
| Reviewer/Fixer | Finds defects and may patch small non-overlapping issues. | Assigned review/fix files only. |
| Security | Reviews security posture, safe test scope, and fail-closed controls. | Security tests/docs/config only unless assigned. |
| UX | Reviews visible workflows, states, accessibility, RTL, responsiveness. | UI/UX files only unless assigned. |
| Senior Monitor | Certifies or rejects the final evidence bundle. | No implementation writes by default. |

All agents work against the same repository and the same accepted task scope. They may use separate worktrees or isolated
execution contexts when the tooling provides them, but their outputs must be integrated back into the same repository by
the orchestrator. Do not create separate product repositories for agent slices unless the user explicitly requests that.

Agents must receive concrete scope, files, constraints, and expected evidence. Do not ask multiple agents to edit the same
file unless the orchestrator deliberately serializes the work.

Every agent must finish by returning a structured result to the orchestrator. A delegated task is not complete until the
agent reports one of these outcomes:

- `DONE`: work completed, changed files listed, checks run, residue stated.
- `FINDINGS_ONLY`: review completed without edits, findings and evidence listed.
- `REWORK_REQUIRED`: defects found that require implementation changes before certification.
- `BLOCKED`: the required evidence, environment, authorization, or dependency was unavailable.

An agent that times out, stops early, or returns no usable result counts as `BLOCKED` for the affected gate.

## Orchestrator responsibilities

- Classify the task, define the blast radius, and decide which roles are required.
- Keep the immediate blocking path local; delegate parallel side work only when it materially advances the task.
- Give each agent a disjoint write set or a read-only review mandate.
- Integrate results against the current tree, not against stale assumptions.
- Resolve conflicts by evidence: tests, runtime readback, diff, architecture rules, security scope, and UX verification.
- Record which agent did what, what evidence was accepted, what was rejected, and what remains.
- Require each agent's final report before Senior Monitor certification. Missing reports are blockers or explicit residue.

## Execution pattern

1. Intake and classification: state task class, risk, target files, and required roles.
2. Architecture map: identify ownership, boundaries, data flow, runtime surface, and UI/security impact.
3. Agent dispatch:
   - Implementer starts the main change.
   - Security and UX can review in parallel when their inputs are stable enough.
   - Reviewer/Fixer can inspect the diff or a scoped slice.
4. Agent return: every agent sends its status, changed files, findings, checks, blockers, and residue.
5. Integration: orchestrator reviews returned changes/findings and applies only supported corrections.
6. Verification: run focused tests, security checks, UI/readback checks, and runtime/container checks where applicable.
7. Senior Monitor: certify final evidence or return `REWORK_REQUIRED`/`BLOCKED`.
8. Closing report: end with **تم إنجازه**, **تم تحسينه**, and **المتبقي**.

## Evidence bundle

The final bundle must include:

- Original request and accepted scope.
- Role map and agents used.
- Each agent's final returned status.
- Changed files and ownership.
- Checks run, blocked checks, and decisive results.
- Security, UX, performance, runtime, and tenant/money/inventory evidence where relevant.
- Final Senior Monitor decision.

## Safety rules

- Subagents never expand scope, authorize production testing, push code, or certify their own changes.
- Security agents must follow authorized defensive boundaries.
- UX agents cannot override domain, security, or accounting invariants.
- Implementer agents cannot mark their own work certified.
- Missing evidence is residue or blocker, not success.
