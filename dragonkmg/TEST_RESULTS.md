# Test results — 2026-10-15

| Command | Exit code | Result |
| --- | ---: | --- |
| `dotnet build dragonkmg/DragonKMG.sln --no-restore --tl:off` | 0 | 0 warnings, 0 errors |
| `dotnet test DragonKMG.sln --no-restore --tl:off` | 0 | 4 focused tests passed, including default-skill seeding and provider response parsing |
| `npm run build` in `dragonkmg/ui` | 0 | TypeScript and Vite production build passed |
| Loopback API smoke | 0 | Health plus persisted role/skill assignment and effective instruction readback passed |
| Cyber KMG safe baseline | 0 | 1 reviewed false positive secret-pattern match; 0 Docker-risk matches; 13 manifests inventoried |
# Verification results — 2026-10-15

- `dotnet build DragonKMG.sln --no-restore --tl:off`: passed with 0 warnings and 0 errors.
- `dotnet test DragonKMG.sln --no-restore --tl:off`: passed 6 of 6 tests, including interrupted-run recovery, council provisioning, and sequential evidence handoff.
- `npm run build`: passed for the production dashboard bundle.
- `npm run lint`: passed with no diagnostics.
- Local API authentication smoke: an unauthenticated `/api/roles` request returned `401`; the same request with the configured local token returned `200`.
- Cyber KMG safe baseline: 1 reviewed false positive (SQLite connection-string variable name), 0 Docker-risk matches, and 13 dependency manifests inventoried.
