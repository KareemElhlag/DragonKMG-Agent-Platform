# DragonKMG conversation-first terminal

## Operator workflow

Open the CLI with `tui <session-name>`. Set DRAGONKMG_DB_PATH to the same database used by the local API; otherwise separate databases legitimately show separate agents.
Set DRAGONKMG_WORKSPACE_ROOT before using /open.

The interactive ANSI console now has:
- Compact brand header, selected agent/mode/model and actual connection/skill/tool/file counts.
- Bounded conversation viewport above a fixed bottom prompt.
- Blue user messages, green assistant responses, amber real process events and red failures.
- Plain text sends a prompt. Only slash-prefixed commands can change state.
- /help, /agents, /use <name>, /model <id>, /mode <mode>, /open <relative-path>, /files, /chat, /report, /clear, /exit.
- Arrow-key editing, Home/End, Up/Down input history and PageUp/PageDown transcript navigation.
- Ctrl+C clears an idle draft; during inference it cancels the provider request and records a terminated run before returning to the prompt.
- Ctrl+D or Ctrl+Z on an empty draft ends the session. Redirected input EOF also exits rather than spinning.

There is no fabricated thinking transcript. Running status means the configured service is processing/awaiting its provider; provider responses appear when complete. Token-by-token streaming is not implemented.
Opening a file previews it; this does not silently grant an agent tools or attach file contents to future prompts.
The terminal cannot display SVG in all emulators. Its compact text mark is a fallback; the canonical dragon+D SVG is used by the web sidebar, header and favicon.

## Safety and boundaries

Provider/session text is stripped of terminal control characters before rendering; names cannot inject frame newlines.
Prompt viewport rendering is cached until transcript content or width changes.
Display limits: 200 entries, 32,000 characters per entry; this is a viewport limit, not deletion from persistent conversation history.
Report filenames use session UUIDs, not untrusted names.
Model/agent policies and secret resolution remain in the existing application service; this UI does not grant tools or weaken cloud approvals.
Cancellation persists Failed / OperationCanceled with a finish time, rather than leaving the agent run Running.
Plain text starting with words like “exit criteria”, “model the workflow” or “open the plan” cannot trigger commands.

## Verification record — 2026-10-15

Classification: FEATURE (interactive input behavior and cancellation state), bounded to CLI presentation/application cancellation and shared web branding; no schema changes.
Owners: CLI terminal surface/input/orchestration; existing PlatformService for run status; one SVG/DragonLogo component for web branding.
Independent reviewer found command ambiguity, Ctrl+C, EOF and terminal-cell issues; corrected and re-reviewed ACCEPT.
Brand implementer returned DONE; two source-contract tests verify one shared asset and decorative accessibility.
Automated suite includes control-character stripping, Unicode grapheme preservation, actual CJK/emoji cell wrapping, bottom prompt placement across three terminal sizes, command isolation and cancellation persistence.
UI build/lint/locale validation and advisory audit run separately; locale count stays 191 per language.
Final evidence: 47/47 .NET tests passed; solution build 0 warnings/0 errors; 2/2 logo tests passed; lint and locale validation passed; advisory UI audit 0 findings; git diff --check clean.
Actual CLI redirected-input smoke /help → /agents → /exit exited 0 with all three configured agents. Updated native session launched (CLI child observed beneath its PowerShell host); visual/IME approval remains a device check.
Browser RTL checks at 1280, 768 and 390 pixel viewport settings found no horizontal overflow (client/scroll widths 1265/1265, 753/753 and 375/375); light and dark logos loaded correctly. API restarted on the rebuilt artifact and retained three agents.
UI JS gzip baseline 91.44 kB; changed 91.57 kB (+0.13 kB). This is a bundle-size comparison, not an interaction-latency measurement.
Browser checks verify loaded logo assets, Arabic layout and no horizontal overflow.
Senior Monitor scripts are inventory checks and return REVIEW_REQUIRED, not automatic certification.

Known residue: terminal color/cell-width behavior depends on emulator and font; native Windows visual approval and macOS/Linux/mobile smoke remain user/device checks. Tiny terminals below 10 columns/8 rows are not a supported layout. Interactive Arabic shaping/bidi and IME behavior require native-terminal verification. No native screenshot tooling was available in this run.
No paid provider calls are required to verify these layout changes.

### Windows launch correction

User screenshot falsified the initial native-visibility claim: the standalone window was in plain mode because it inherited TERM=dumb from the desktop launcher. Windows console detection now ignores that POSIX hint when both console streams are interactive; real redirected streams still use plain mode. Five regression cases cover Windows, POSIX and redirected input/output. Plain mode also prints an explicit input prompt rather than leaving an unexplained blank window. Native visual approval must be obtained from the corrected run, not the earlier launch.
Rollback: revert the named CLI presentation/input changes, cancellation handlers and logo bindings; no data migration rollback is needed. Existing sessions and credentials are untouched.
