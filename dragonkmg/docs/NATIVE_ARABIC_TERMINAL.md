# Native Arabic terminal rendering — 2026-10-15

This supersedes the earlier browser-only Arabic limitation for the Windows CLI renderer. The browser companion remains optional; native Arabic no longer requires opening it.

## Scope and ownership

BUGFIX: CLI presentation and caret movement only. No provider, credential, database, API, stored message or report format changes.
TerminalTextLayout uses the operating-system ICU C API for Unicode bidirectional levels/maps, bracket mirroring and contextual Arabic shaping.
NativeIcuBidi resolves the installed system library; no new NuGet package, vendored shaping table or downloaded executable is introduced.
Windows loads the absolute system icu.dll path; current machine availability was asserted in tests. Linux/macOS binding probes exist but were not runtime-verified.

## Behavior

- Shapes Arabic letters into connected presentation forms and reorders visual grapheme clusters, not the original string.
- Determines paragraph direction from the first strong character; Latin headers stay left-to-right.
- Keeps Latin model IDs, numbers, emoji sequences and combining marks intact.
- Preserves logical text in drafts, input history, model requests, saved sessions and reports.
- Uses a fixed-length shaping path without optional lam-alef collapse, retaining cursor positions.
- Shares terminal-cell width calculation between layout, clipping and cursor positioning.
- Maps visual left/right arrows to logical grapheme boundaries; Backspace/Delete operate on logical input.
- Right-aligns Arabic-first lines and prompt content. Horizontal viewport follows the visual caret.
- Wraps at word boundaries when possible; tokens longer than a full row still require splitting.
- Missing ICU yields a clear rendering warning; navigation retains a safe logical-boundary fallback.
- Plain redirected output is intentionally logical text, not baked display glyphs.
- Copying text from a legacy terminal screen may copy visual presentation forms; reports retain the original logical Unicode.

## Verification

- 70/70 .NET tests passed on Windows, including explicit system ICU availability, literal expected forms for تم and مرحبا, lam-alef, combining marks, ZWNJ, mixed model-id/123, emoji, visual cursor bounds, Arabic arrow movement and word wrapping.
- Native full-frame probe retains model-id and contains shaped Arabic glyphs. Warm 100-frame average with one Arabic transcript entry and a mixed prompt at 120x30: 2.28 ms; this is a focused local sample, not a cross-device P95 benchmark.
- Interactive PTY smoke loaded the existing demo-team Arabic messages and emitted visual presentation forms with right alignment.
- Source review resolved missing-ICU arrow handling and inconsistent emoji/join-control widths; independent decision CERTIFIED_WITH_RESIDUE.
- No paid inference or provider call was required.

Remaining validation: visible native-window approval with the user's chosen font; physical keyboard/IME behavior and macOS/Linux/mobile terminals remain device checks. Do not interpret automated glyph/caret checks as a screenshot-level certification of every emulator.
Rollback: restore the prior CLI text surface and remove the two layout source files; no persisted data rollback is needed.

Primary API references:
- [ICU bidi C API](https://unicode-org.github.io/icu-docs/apidoc/released/icu4c/ubidi_8h.html)
- [ICU Arabic shaping](https://unicode-org.github.io/icu-docs/apidoc/released/icu4c/ushape_8h.html)
- [Windows system ICU](https://learn.microsoft.com/en-us/windows/win32/intl/international-components-for-unicode--icu-)
