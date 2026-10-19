# Project skills

This directory owns portable project copies of the default engineering skills and specialist skills:

- `kmg-agent-skill/SKILL.md`
- `cyber-kmg-security/SKILL.md`
- `nexusecore-ui-ux/SKILL.md`
- `flutter-architect-review/SKILL.md`
- `nexus-feature-performance/SKILL.md`
- `nexus-master-audit-skill/SKILL.md`

Original attribution, licenses, references, templates, and helper scripts are preserved. Local configuration, field lessons, temporary evidence and security logs are excluded. The installed skills under the user's Codex directory are unchanged; project copies are edited independently.

## Feature production routing

For a substantial NexusCore feature, KMG remains the lifecycle and evidence authority. Add specialist skills only when their scope is relevant:

1. `nexus-feature-performance` guides planning and implementation choices for hot paths, concurrency, event flow, memory, and context/token cost.
2. The platform's UI skill or `flutter-architect-review` guides the chosen client surface; do not apply Flutter guidance to web-only work.
3. `cyber-kmg-security` provides security review when authentication, authorization, tenant boundaries, secrets, integrations, or hostile input are in scope.
4. `nexus-master-audit-skill` performs an evidence-based readiness audit after implementation and focused specialist reviews. It does not replace the Senior Monitor or certify unrun checks.

The user/orchestrator chooses a single agent or a team per task. A planning agent may propose ownership and order, but agents execute only after the orchestrator assigns disjoint write sets. Recommended flow for broad features: Plan → Implement → Review/repair → Performance and UX/security reviews as applicable → readiness audit → KMG Senior Monitor. Run independent stages in parallel only when prerequisites are stable and write sets do not overlap. Small isolated changes should use a single pass to avoid unnecessary model calls and coordination overhead.

These markdown skills are project-owned instructions, not runtime enforcement. Tool grants, approval checks, and team execution order must still be enforced by the platform engine; a skill file cannot grant tools or guarantee that agents ran.

## Using a file skill

In Dashboard → Skills, select **Skill file path** and enter a path such as `kmg-agent-skill/SKILL.md`. Paths are resolved beneath the configured skills directory. Each effective-configuration build reads the entire UTF-8 Markdown entry file again; editing that project file changes the next run without modifying the database record.

The full entry-file text and its SHA-256 digest are incorporated into the run's effective instructions. Supporting references are shipped beside the entry file; the resolver does not automatically concatenate every reference or execute helper scripts. Tools still require independent grants and approvals.

From the CLI: `dragonkmg skill add-file my-skill my-skill/SKILL.md`.

The local development API resolves `dragonkmg/skills`. Published API/CLI output includes a `skills` directory as content. Use `DRAGONKMG_SKILLS_ROOT` to select a different approved root. Symlinks/reparse points, paths outside the root, non-Markdown files, invalid UTF-8, empty files, and files larger than 256 KiB are rejected.

## Using inline instructions

Select **Inline instructions** to write the complete instruction text in the dashboard. File and inline modes can be changed using **Edit skill**. Existing IDs keep agent assignments intact. Skill updates use an atomic version check to reject concurrent overwrites.

## Nexus Feature Full Production team

Teams offers the `Nexus Feature Full Production` template for large NexusCore features. It provisions eight ordered roles: Planner, Implementer, Test & Compatibility, Platform UX, Performance, Master Auditor, Security Reviewer, and Release Certifier. The team uses the selected enabled connection/model for newly created members and assigns file-backed project skills by role. A fixed evidence-weighted readiness rubric is included in the Release Certifier instructions.

The provision endpoint fails closed if its connection/model, system roles, or required skills are missing or disabled. Re-provisioning returns an existing team without replacing member configuration. UX guidance includes both web and Flutter skills; until conditional routing exists, the UX agent must inspect manifests and apply only the relevant stack guidance. Team execution is sequential and can consume up to eight model calls, so small isolated tasks should use a single agent. See [the full Arabic workflow and limits](docs/NEXUS_FEATURE_FULL_PRODUCTION.ar.md).

## Default team

Agents displays Execution, Quality Review, and Security Test team templates. Choose an enabled provider connection and a real model ID, then create the council. All enabled default skills are assigned to newly created members. Review members use Review mode. Existing members retain custom connection/model bindings and skill assignments.

A team cannot execute until a real provider connection and model are configured. No placeholder provider, credentials, or model is invented.

## Local verification

- .NET build: zero warnings/errors; 16 tests pass.
- Tests cover full file loading and rereading after edits, path escape/extension/size/encoding rejection, user-edit preservation, atomic version conflicts, and complete default instructions in the council runtime.
- Browser readback displayed the complete 17,898-character KMG entry file and successfully saved/read back its default project path.
- UI build, lint and locale parity checks pass (162 keys in each of four languages).
- Independent source review returned ACCEPT. Safe baseline reported two reviewed heuristic matches and zero Docker-risk matches.
