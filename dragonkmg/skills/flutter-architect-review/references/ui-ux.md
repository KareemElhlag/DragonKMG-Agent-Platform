# Flutter UI/UX Reference

Use this reference before creating or refactoring Flutter screens, flows, components, design systems, responsive behavior,
Arabic/RTL support, accessibility, or visual quality gates.

## ERP Interface Principles

- Make the first screen useful, not promotional. Operational users need data, filters, actions, alerts, and clear state.
- Favor dense but breathable layouts: compact rows, clear hierarchy, stable columns, visible totals, and consistent spacing.
- Use status badges for operational states: draft, pending, approved, posted, rejected, low stock, synced, offline, overdue.
- Keep primary actions close to the object they affect. Destructive or financial actions require confirmation and server
  authority.
- Every data surface needs loading, empty, error, retry, permission-denied, and stale/offline states where applicable.
- Forms should show field-level validation, server validation, dirty/saving state, unsaved-change protection, and a clear
  success/next-step result.

## Flutter Layout Rules

- Use constraints deliberately. Avoid layout that only works on the developer's phone size.
- Prefer `CustomScrollView` with slivers for complex scrollable screens instead of nested scrollables fighting each other.
- Use lazy builders for long lists and grids.
- Keep expensive work out of `build()`. Precompute in view models or memoized selectors where appropriate.
- Break large widgets by responsibility: shell, filters, list/table, row/item, form sections, action bars, state views.
- Use design-system components for repeated ERP patterns: money text, quantity text, warehouse badge, stock indicator,
  branch selector, permission-aware action button, retry panel, and skeleton rows.

## Responsive Behavior

- Compact phone: prioritize search/filter, primary status, key quantity/money values, and one-handed actions.
- Regular phone: expose secondary fields and batch actions when safe.
- Tablet/desktop: use split panes, side filters, data tables, sticky action bars, and keyboard-friendly focus.
- Never rely on hover-only controls. Mobile and tablet users need visible or discoverable actions.
- Text must survive large accessibility font settings. Important values should wrap or reflow, not clip.

## Arabic, RTL, And Localization

- Use Flutter localization tooling and avoid hard-coded display strings in production screens.
- Use `Directionality`, locale-aware formatting, and start/end alignment instead of left/right for UI layout.
- Keep SKU, serial number, invoice number, and technical electrical terms readable in mixed Arabic/English contexts.
- Format numbers, dates, currencies, and units by locale and business policy.
- Test RTL screens for icon direction, table alignment, drawer/navigation behavior, and truncated labels.

### Bidi/RTL correctness: a concrete widget-test pattern

A RichText/TextSpan row that mixes a currency symbol or a leading sign with a numeric run (e.g. money text,
signed deltas, invoice numbers mixed with Arabic labels) can have correct widget order in the tree while still
rendering the GLYPHS in the wrong visual order — `flutter analyze`, a green `flutter test` on the widget's logic,
and even a correct `Directionality`/`textDirection` setting on the containing widget do not catch this, because
none of them look at actual rendered glyph *positions*. This exact class of bug has hit this project on both the
web side (fixed with an explicit `dir="ltr"` wrapper around the numeric run) and the mobile side (no existing
tooling caught it; the fix required measuring where characters actually land on screen). Use this pattern instead
of reinventing it per review:

1. Render the real widget (not a stub) inside a `MaterialApp` with `Locale('ar')` and `Directionality(textDirection: TextDirection.rtl, ...)` in a widget test — do not only test it under `TextDirection.ltr`, since that is exactly the condition that hides the bug.
2. Use `tester.getTopLeft(find.text('<expected leading character or digit>'))` (or `RenderParagraph.getBoxesForSelection` for a specific character range within one Text/RichText) to get the actual on-screen X position of the first character you expect to be visually leftmost/rightmost, and assert its position relative to the other characters' positions — not just that the text exists.
3. For a value built from multiple spans (symbol + amount, or sign + amount), assert that the span you expect visually first (per the LTR-numeral-inside-RTL-sentence rule, usually the numeral run as a whole, kept together) has a `getTopLeft().dx` on the correct side of the span you expect second. A passing widget-existence test with no position assertion proves nothing about bidi correctness.
4. Wrap any span whose internal character order must stay LTR regardless of surrounding direction (currency amounts, serials, SKUs) in its own `Directionality(textDirection: TextDirection.ltr, child: ...)`, mirroring the web fix's `dir="ltr"` wrapper — do not rely on Unicode bidi control characters alone; they are inconsistent across Flutter's text layout vs. a browser's.
5. This check belongs in the Visual Review Gates below for any screen carrying money, signed deltas, or mixed Arabic/English identifiers — not only on screens explicitly described as "RTL work."

### A web-renderer check is not proof for the native engine

`flutter test`'s default test environment renders through Skia's software/web-adjacent test harness, not the
real engine a device uses (Impeller on iOS/recent Android, or the platform's native text shaping). A bidi/glyph
assertion that passes under `flutter test` is evidence, not proof, for how the real app renders on a physical
device — font fallback, complex-script shaping (Arabic ligatures/kerning), and RTL mirroring can differ on-device.
Treat a widget-test-level glyph-position assertion (above) as the mandatory minimum gate, and still name the
on-device verification as explicitly unverified/pending in the review's output whenever a real Android/iOS run
was not performed — do not imply device-level confidence from test-harness evidence alone.

## Accessibility

- Use semantic labels for icon-only actions, status indicators, charts, and custom controls.
- Preserve focus order and keyboard traversal for tablet/desktop and accessibility users.
- Keep touch targets large enough for operational use in warehouses and branches.
- Use color plus text/icon shape for status; never make color the only signal.
- Check contrast for badges, disabled states, and selected rows.

## Visual Review Gates

Before claiming a Flutter UI improvement is done, verify or state unverified:

- Main workflow renders at compact and regular phone widths.
- Loading, empty, error, and success states are reachable.
- Long Arabic and English strings do not overlap critical values or actions.
- Text scaling does not break primary actions.
- Lists remain smooth with representative data size.
- Permission-denied and offline/sync states explain what the user can do next.
- Any money, signed-delta, or mixed Arabic/English identifier text has a real RTL widget test asserting glyph
  *position* (see "Bidi/RTL correctness" above), not just widget existence — and on-device verification is
  named explicitly as unverified if it was not actually performed.

## References

- Flutter accessibility: https://docs.flutter.dev/ui/accessibility
- Flutter internationalization: https://docs.flutter.dev/ui/internationalization
- Flutter performance best practices: https://docs.flutter.dev/perf/best-practices
