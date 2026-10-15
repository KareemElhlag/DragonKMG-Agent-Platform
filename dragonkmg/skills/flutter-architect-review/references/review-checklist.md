# Flutter/Dart Review Checklist

Use this reference when the user asks for review or when a Flutter/Dart change has enough risk to need a structured pass.
Findings come first, ordered by severity, with file and line references when available.

## Severity Guide

- P0: data loss, wrong ledger/stock/money result, auth bypass, tenant leak, crash on critical workflow, irreversible action
  without server validation.
- P1: broken primary user flow, incorrect state transition, missing permission/error handling, repeated duplicate writes,
  severe performance regression, inaccessible critical control.
- P2: maintainability or UX defect likely to create bugs soon: boundary leak, untestable orchestration, inconsistent empty
  or retry state, fragile navigation, weak typing, avoidable rebuilds on important screens.
- P3: style, naming, minor consistency, small cleanup, or future improvement.

## Review Order

1. Business correctness: ERP rules, tenant/branch/warehouse context, permissions, money, stock movements, fiscal state.
2. Architecture: dependency direction, feature boundaries, domain purity, repository contracts, DTO mapping.
3. State and async: loading/error/empty/saving/success states, cancellation, stale responses, retries, optimistic updates.
4. UI/UX: layout density, readability, touch targets, table/list ergonomics, status badges, form validation, mobile behavior.
5. Accessibility and localization: semantics, labels, focus order, text scaling, contrast, RTL, localized strings.
6. Performance: rebuild scope, list virtualization, image sizing/caching, intrinsic layout, sync work, repeated calls.
7. Tests: unit tests for business logic, widget tests for state rendering, integration tests for critical flows.
8. Dart quality: null safety, immutability, effective naming, imports, generated code boundaries, analyzer warnings.

## Flutter-Specific Defects To Hunt

- Repository/API calls inside `build()` or widget constructors.
- `setState` after disposal, uncancelled streams/timers/controllers, or ignored mounted checks after awaits.
- Nullable fields representing mutually exclusive states instead of explicit state types.
- `double` for money or quantities where precision/rounding rules matter.
- Local filtering/sorting that conflicts with server paging or warehouse permission scope.
- Unbounded `ListView`/`GridView` inside scrollables, `shrinkWrap` used as a blanket fix, or expensive intrinsic sizing.
- Large widgets mixing layout, API calls, validation, navigation, formatting, and business rules.
- Catch-all error messages that hide retryability, permission denial, validation errors, or offline state.
- Text literals on screens that should be localized.
- Tap targets below platform guidance, missing semantics for icon-only actions, and layouts broken by text scaling.

## KMG-Aligned Evidence

For NexusCore or production ERP work, pair this review with `$kmg-agent-skill`:

- State the task class and blast radius.
- Verify the touched files and current diff before claiming review coverage.
- Record which checks were run and which were not.
- Treat missing evidence as unverified, not passed.
- End completed implementation tasks with KMG's required closing report sections when that skill is loaded.

## Review Response Shape

Use this shape unless the user requested another format:

```text
Findings
- [P1] File/path.dart:42 - Specific defect and why it matters.

Open Questions
- Any unresolved product, domain, or runtime assumption.

Tests / Evidence
- Checks performed or not performed.
```

If there are no findings, say so clearly and still mention residual risk or missing verification.

## References

- Effective Dart: https://dart.dev/effective-dart
- Flutter performance best practices: https://docs.flutter.dev/perf/best-practices
- Flutter accessibility: https://docs.flutter.dev/ui/accessibility
