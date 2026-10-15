---
name: flutter-architect-review
description: Build, refactor, and review production Flutter/Dart apps with clean architecture, fast Dart practices, UI/UX quality gates, and KMG-style evidence. Use for Flutter code, app architecture, state management, performance, or mobile ERP interface work.
metadata:
  short-description: Flutter/Dart architecture, review, and UI/UX quality gates
---

# Flutter Architect Review

Use this skill when a task touches Flutter, Dart, mobile app architecture, state management, Flutter performance,
or the UI/UX of a Flutter app. It is especially intended for NexusCore ERP mobile and branch workflows where dense,
accurate operational screens matter more than decorative layouts.

This skill complements `$kmg-agent-skill`: KMG owns lifecycle discipline, evidence, security, data integrity,
and fail-closed review. This skill owns Flutter/Dart architecture, widget boundaries, state flow, mobile UX,
performance, accessibility, localization, and review criteria. For NexusCore visible workflows, also load
`$nexusecore-ui-ux` when React or shared ERP design language is relevant.

## Operating Rules

- Read the smallest project evidence that can answer the architecture question: `pubspec.yaml`, `analysis_options.yaml`,
  feature folders, routing setup, state-management setup, and the touched screens/view models/repositories.
- Preserve the app's existing architecture unless it is actively causing testability, performance, state, or UX defects.
- Prefer feature-first organization for production apps: each feature owns presentation, state/view model, domain/use cases
  when present, and data adapters. Shared code must be genuinely cross-feature.
- Treat UI state as explicit data, not scattered widget booleans: loading, empty, error, stale, saving, optimistic, and
  permission-denied states should be represented deliberately.
- Keep widgets mostly declarative. Business rules, orchestration, filtering, validation that affects data, and API calls
  belong outside widgets.
- Use Dart's type system aggressively: immutable value objects, sealed result/state types where appropriate, non-nullable
  fields by default, and no dynamic maps crossing feature boundaries without typed DTOs or models.
- Use `Money` or a project-approved equivalent for all currency values in ERP surfaces. Never format, round, or compare
  money as raw `double`.
- Do not invent tenants, currencies, warehouse ids, locale assumptions, or permission defaults. Unknown operational facts
  remain explicit unknowns.
- Prefer existing packages already adopted by the app. Add a package only when it removes real complexity, has active
  maintenance, fits the app's architecture, and is justified in the response.
- For design claims, verify at the relevant viewport classes: compact phone, regular phone, tablet/desktop if the app
  targets them, and RTL when Arabic is supported.

## Architecture Defaults

Use Flutter's current official architecture direction as the baseline: separation of concerns, MVVM-style UI layer,
repositories/services in the data layer, and a domain/use-case layer when business logic is complex. The official
Flutter docs describe views, view models, repositories, and services as the recommended scalable baseline, with an
optional domain layer for complex logic.

Read [references/architecture.md](references/architecture.md) before proposing or changing architecture, folder layout,
dependency direction, state management, navigation, offline/cache strategy, or package choices.

## Review Mode

When the user asks for review, lead with findings ordered by severity. Inspect behavior, architecture boundaries,
state correctness, async/error handling, performance, accessibility, localization, tests, and ERP data integrity before
style nits. A missing loading/error/empty path on an operational screen is a product defect, not cosmetic feedback.

Read [references/review-checklist.md](references/review-checklist.md) for Flutter/Dart review criteria. If the task is
also a NexusCore or ERP repository change, load `$kmg-agent-skill` and use its closing evidence expectations.

## UI/UX Mode

For Flutter UI work, build the actual operational experience first. Favor dense, readable screens, predictable navigation,
clear status badges, table/list ergonomics, accessible touch targets, skeleton/loading states, retryable error states,
and empty states that explain the next action. Avoid marketing-page patterns inside ERP workflows.

Read [references/ui-ux.md](references/ui-ux.md) before designing or refactoring Flutter screens, components, responsive
layouts, Arabic/RTL behavior, or accessibility.

## Performance And Quality

- Run or recommend the narrowest meaningful checks first: `dart format`, `dart analyze`, focused unit/widget tests, and
  integration tests for critical flows.
- For performance-sensitive screens, look for excessive rebuilds, intrinsic layout passes, unbounded lists, large sync work
  on the UI isolate, unnecessary image memory, nested scroll conflicts, and repeated repository calls from `build()`.
- Use `const` constructors where they reduce rebuild work and clarify immutability, but do not turn `const` into a cosmetic
  review nit when larger defects exist.
- Prefer widget tests for rendering states, unit tests for view models/use cases/repositories, golden tests for stable
  design-system components, and integration tests for high-value branch workflows.

## Source Baseline

The guidance in this skill is aligned with official Flutter and Dart documentation checked on 2026-10-05:
Flutter app architecture, Flutter performance best practices, Flutter accessibility and internationalization, and
Effective Dart. When the user explicitly asks for the latest framework behavior, verify against official docs before
making version-specific claims. Prefer primary sources such as `docs.flutter.dev`, `dart.dev`, and package maintainers'
own documentation over blogs, social posts, or trend summaries.
