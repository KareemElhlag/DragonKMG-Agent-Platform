# Project skills

This directory owns the portable project copies of the four default skills:

- `kmg-agent-skill/SKILL.md`
- `cyber-kmg-security/SKILL.md`
- `nexusecore-ui-ux/SKILL.md`
- `flutter-architect-review/SKILL.md`

Original attribution, licenses, references, templates, and helper scripts are preserved. Local configuration, field lessons, temporary evidence and security logs are excluded. The installed skills under the user's Codex directory are unchanged; project copies are edited independently.

## Using a file skill

In Dashboard → Skills, select **Skill file path** and enter a path such as `kmg-agent-skill/SKILL.md`. Paths are resolved beneath the configured skills directory. Each effective-configuration build reads the entire UTF-8 Markdown entry file again; editing that project file changes the next run without modifying the database record.

The full entry-file text and its SHA-256 digest are incorporated into the run's effective instructions. Supporting references are shipped beside the entry file; the resolver does not automatically concatenate every reference or execute helper scripts. Tools still require independent grants and approvals.

From the CLI: `dragonkmg skill add-file my-skill my-skill/SKILL.md`.

The local development API resolves `dragonkmg/skills`. Published API/CLI output includes a `skills` directory as content. Use `DRAGONKMG_SKILLS_ROOT` to select a different approved root. Symlinks/reparse points, paths outside the root, non-Markdown files, invalid UTF-8, empty files, and files larger than 256 KiB are rejected.

## Using inline instructions

Select **Inline instructions** to write the complete instruction text in the dashboard. File and inline modes can be changed using **Edit skill**. Existing IDs keep agent assignments intact. Skill updates use an atomic version check to reject concurrent overwrites.

## Default team

Agents displays Execution, Quality Review, and Security Test team templates. Choose an enabled provider connection and a real model ID, then create the council. All enabled default skills are assigned to newly created members. Review members use Review mode. Existing members retain custom connection/model bindings and skill assignments.

A team cannot execute until a real provider connection and model are configured. No placeholder provider, credentials, or model is invented.

## Local verification

- .NET build: zero warnings/errors; 16 tests pass.
- Tests cover full file loading and rereading after edits, path escape/extension/size/encoding rejection, user-edit preservation, atomic version conflicts, and complete default instructions in the council runtime.
- Browser readback displayed the complete 17,898-character KMG entry file and successfully saved/read back its default project path.
- UI build, lint and locale parity checks pass (162 keys in each of four languages).
- Independent source review returned ACCEPT. Safe baseline reported two reviewed heuristic matches and zero Docker-risk matches.
