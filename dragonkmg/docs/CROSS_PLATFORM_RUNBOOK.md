# Cross-platform runbook

## Current distribution status

DragonKMG does not yet ship signed installers, a Homebrew formula, Linux packages, or mobile app bundles. Until the paid-edition distribution work is complete, use a manually downloaded, architecture-matched GitHub Release archive and verify its published SHA-256 checksum before running it.

Never run an unverified `curl | bash` installer. An automated installer is a paid-edition deliverable and must be signed, versioned, checksum-verified, and independently tested before release.

## Release artifact naming

The intended self-contained release artifacts are:

| Platform | Artifact |
| --- | --- |
| Windows x64 | `dragonkmg-win-x64.zip` |
| Windows ARM64 | `dragonkmg-win-arm64.zip` |
| macOS Intel | `dragonkmg-mac-x64.tar.gz` |
| macOS Apple Silicon | `dragonkmg-mac-arm64.tar.gz` |
| Linux x64 | `dragonkmg-linux-x64.tar.gz` |
| Linux ARM64 / Termux-compatible target | `dragonkmg-linux-arm64.tar.gz` |

These are design targets; no archive is claimed published until a GitHub Release and checksum evidence exist.

## Manual launch

1. Download the archive matching your operating system and CPU architecture from the official project release page.
2. Verify the matching `SHA256SUMS` entry published with that release.
3. Extract to a directory owned by the current user.
4. Run `dragonkmg` with no arguments to enter the default persisted session.

The CLI stores user data under the operating system's local application-data directory. Back up the DragonKMG directory before replacing a release or allowing a database migration.

## Mobile terminal guidance

Android terminals can be used when a supported ARM64 .NET runtime and artifact are available. Treat this as an advanced terminal-control scenario, not a replacement for a mobile dashboard. iOS/iPadOS does not support the same local CLI distribution model; use a future native client or a securely hosted dashboard.

## Paid-edition installer acceptance gates

- Signed artifacts plus published checksums and provenance.
- Interactive install, upgrade, rollback, and uninstall verification on named Windows, macOS, and Linux environments.
- Backup before SQLite schema migrations; no data loss on failed upgrade.
- Per-platform vault adapter validation and safe `env:` fallback.
- Offline install behavior and licensing grace period tested without transmitting customer data.
