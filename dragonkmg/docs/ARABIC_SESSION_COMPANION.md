# Arabic session companion — 2026-10-15

User job: preserve a terminal workflow while Arabic text, mixed code identifiers and a real vector logo have a correct graphical surface. Terminal emulators are not claimed to provide universal Arabic shaping.

## Delivered workflow

- Open Dashboard > Sessions, or /arabic from the CLI.
- /arabic opens http://127.0.0.1:5173/?session=<UUID>#sessions without credentials in the URL.
- Browser requires the existing local API token. It reads/writes the same SQLite sessions as the CLI.
- The canonical dragon+D SVG appears in the sidebar, header and session panel. The misleading terminal D symbol and “dragon + D” filler have been removed. A terminal text canvas is not claimed to display arbitrary SVG images.
- Arabic input/messages use natural browser shaping, dir=auto and unicode-bidi:plaintext; mixed Latin identifiers remain raw text.
- Conversation is chronological, scrollable above a bottom composer. Enter sends, Shift+Enter inserts a line, IME composition does not send.
- Agent/model selection, create session, refresh, real waiting status, stop and visible errors are functional.
- Failed sends preserve the draft and refresh persisted history without automatic provider retry.
- Replies remain complete-response, not token streaming; no fabricated chain-of-thought display.
- History read is bounded to the latest 100 messages. Provider calls currently receive the submitted message and effective agent instructions, not an automatic concatenation of the entire conversation.

## Ownership, permissions and safety

SessionConversationService owns both CLI and browser persistence. API route is behind existing authentication; provider policies/tool grants remain unchanged.
Input is limited to 16,000 characters. Shared SQLite lease prevents overlapping sends from separate CLI/API processes; requests time out at 90 seconds, lease expires at 3 minutes for crash recovery, and owner-scoped release cannot remove a successor lease.
Session selection is re-read by the CLI before sending and on exit to avoid overwriting a browser selection.
Responses are rendered as text, not HTML. Safe failure records exclude upstream exception details.
The new session_request_leases table is additive. On rollback existing sessions/messages stay intact; leaving the unused table is safe.

## Evidence

- .NET: 57/57 passing, solution build 0 errors / 0 warnings.
- New tests: Arabic persistence, safe failures, invalid session/oversize rejection, separate-store concurrent rejection, stale lease ownership.
- UI: build/lint pass; four locales 212 keys each; 3 source-contract tests for IME/RTL/text/API wiring.
- Audit: 0 findings; git diff --check clean.
- Browser: actual Arabic test sent through configured free router, response “تم استلام الرسالة.”; API readback and reload confirmed both saved messages.
- 390x844: width/scroll width 390/390, composer bottom815px, transcript277px; 695x668: composer bottom639px. Arabic computed direction rtl.
- Bundle gzip baseline91.57kB →94.73kB (+3.16kB); this is a size metric, not an interaction-latency benchmark.

Remaining device checks: native terminal bidi/IME still depend on emulator and font; use the browser companion for Arabic. macOS/Linux/mobile native launch and non-default dashboard ports require environment-specific validation/configuration. Authentication in a newly opened browser profile is manual; no token is put in the URL.
