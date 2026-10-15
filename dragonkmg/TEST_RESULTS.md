# Test results — 2026-10-15

| Command | Exit code | Result |
| --- | ---: | --- |
| `dotnet build dragonkmg/DragonKMG.sln --no-restore --tl:off` | 0 | 0 warnings, 0 errors |
| `dotnet test DragonKMG.sln --no-restore --tl:off` | 0 | 4 focused tests passed, including default-skill seeding and provider response parsing |
| `npm run build` in `dragonkmg/ui` | 0 | TypeScript and Vite production build passed |
| Loopback API smoke | 0 | Health plus persisted role/skill assignment and effective instruction readback passed |
| Cyber KMG safe baseline | 0 | 1 reviewed false positive secret-pattern match; 0 Docker-risk matches; 13 manifests inventoried |
