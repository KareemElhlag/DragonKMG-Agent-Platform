# Test results — 2026-10-15

## Explicit project files increment (latest)

- Solution build0warnings/errors;116passed,1skipped,total117. The skip is Linux case-sensitive-path behavior on Windows, not a passed platform check.
- UI build/lint passed,277keys4languages,17tests passed. JSgzip104.35kB versus101.47kB before file-context (~2.8% asset increase, not a latency benchmark).
- Real temporary-file tests cover absolute/relative reads, hash, strictUTF8, size, secrets/path/ancestor-link boundaries. Fake-gateway tests prove file-body delivery, original-message isolation, all-member grants and stale scope rejection before reads/calls.
- Running API and CLI both reject README preview under the existing ungranted Planning team with FileAccessDenied. Browser path entry/localized refusal/egress checkbox verified. Existing grants and old16-message chat were preserved; no file sent to a live cloud provider for testing.
- Senior Monitor CERTIFIED_WITH_RESIDUE: positive live preview/provider verification awaits an explicit real grant by the user. File contents persist in AgentRun.Input; hard-link/concurrent-filesystem and mounted-network residual risks are documented in docs/PROJECT_FILES.ar.md.

## Standalone workspace/history increment (latest)

- Solution build:0 warnings/errors;82/82 .NET tests. New history tests cover235 equal-timestamp messages without pagination gaps and separate-session isolation; project binding survives store reopen.
- UI build/lint/locales passed:261 keys per language;15 tests including restoration/routing/source-contract checks. JS gzip101.47kB versus97.41kB before workspace redesign (~4.2% asset increase, not a measured interaction-latency regression).
- LiveAPI: original16 messages preserved and enumerated across4 pages of5; unknown session404. No database relocation or message deletion.
- Livebrowser: old16 messages return after switching sessions, reload, back/forward and base-URL restoration. Project create/bind/reload and per-session in-memory drafts verified. Search and mobile sidebar close verified.
- RTL390 and768 had no horizontal overflow; composer fits390x844 (bottom800.85). Desktop1280 and light/dark were inspected; light neutral-button contrast was corrected. UI audit0 findings.
- Independent review CERTIFIED_WITH_RESIDUE: no durable draft backup, data migration, device sync, active file upload or new permissions. Keep API and CLI pointed at the same database; see docs/WORKSPACE_CHAT.ar.md.

## Named teams verification (latest increment)

- Solution build: 0 warnings, 0 errors. CLI rebuilt after report-label refinement, also clean.
- `dotnet test DragonKMG.sln --no-build --nologo`: 79/79 passed, including six new team-routing/persistence/order/failure/conflict cases. Earlier counts below are historical.
- UI production build, lint and locale parity passed: four languages, 235 keys; nine source-contract tests passed. JS gzip 97.41 kB versus prior 95.08 kB (asset size, not a latency benchmark).
- Live browser: created planning team; verified ordered-member edits and save/readback; default save/clear/reload; missing-team question; Arabic `/team` prompt with a labelled local greeting. RTL390 had scrollWidth390 and composer bottom815 within viewport844; desktop1280 also checked.
- Authenticated API rejects an unrouted request with400/TeamRequired. CLI lists teams and persists a default by full ID and Arabic name with UTF-8 redirected input.
- Local setup now has planning, review, and execution/review teams. Planning uses a PlanOnly agent; configuration is local SQLite data, not a committed fixture.
- Independent Senior Monitor: CERTIFIED_WITH_RESIDUE. No live multi-provider chain or cost/performance guarantee for large teams; fake-gateway tests verify sequencing/failure. Native interactive terminal display was not re-verified visually in this increment.
- Migration is additive (`teams`, `session_teams`). Rollback can retain these unused tables; do not delete the database to roll back. Legacy direct-agent library calls remain for compatibility, but session API/CLI chat now requires team routing.

| Command | Exit code | Result |
| --- | ---: | --- |
| `dotnet build dragonkmg/DragonKMG.sln --no-restore --tl:off` | 0 | 0 warnings, 0 errors |
| `dotnet test DragonKMG.sln --no-restore --tl:off` | 0 | 4 focused tests passed, including default-skill seeding and provider response parsing |
| `npm run build` in `dragonkmg/ui` | 0 | TypeScript and Vite production build passed |
| Loopback API smoke | 0 | Health plus persisted role/skill assignment and effective instruction readback passed |
| Cyber KMG safe baseline | 0 | 1 reviewed false positive secret-pattern match; 0 Docker-risk matches; 13 manifests inventoried |
# Verification results — 2026-10-15

- `dotnet build DragonKMG.sln --no-restore --tl:off`: passed with 0 warnings and 0 errors.
- `dotnet test DragonKMG.sln --no-restore --tl:off`: passed 16 of 16 tests, including interrupted-run recovery, council provisioning, sequential evidence handoff, workspace file-reader boundaries, Windows DPAPI vault round-trip, approval gating, n8n policy, MCP trust boundaries, and workflow-DAG cycle validation.
- `npm run build`: passed for the production dashboard bundle.
- `npm run lint`: passed with no diagnostics.
- Local API authentication smoke: an unauthenticated `/api/roles` request returned `401`; the same request with the configured local token returned `200`.
- Cyber KMG safe baseline: 2 reviewed false positives (SQLite connection-string variable name and a test-only fixture), 0 Docker-risk matches, and 13 dependency manifests inventoried.
- Full skill files: fresh content reload, traversal/size/UTF-8 checks, seed customization preservation, atomic version conflicts, and full default runtime instruction assertions passed.
- Locale parity: 162 keys per language across Arabic, English, Spanish and German. UI audit: zero findings.
- Browser skill verification: full KMG entry preview (17,898 characters), default path save/readback (v3), and inline/file source switching passed.
