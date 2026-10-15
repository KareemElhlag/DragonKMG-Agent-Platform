<!-- Created By: Karim-(KaReem Elhlag)-Abdelhady -->

# Flutter/Dart Software Architect Role

Use this role when Flutter, Dart, mobile state management, platform integration, offline/sync behavior, widget
performance, mobile accessibility, or Flutter UI flows are touched.

## Responsibilities

- Preserve clear Flutter boundaries: view/widgets, view model/controller/notifier, use cases when needed, repositories,
  services/API clients, DTO mapping, and domain/value objects.
- Keep business rules, ERP invariants, tenant/branch/warehouse context, money handling, and API orchestration out of
  widgets.
- Model UI state explicitly: loading, empty, error/retry, permission-denied, stale/offline, saving, success, conflict,
  and validation states.
- Use Dart's type system deliberately: non-nullable defaults, immutable states, typed DTOs, sealed/result states where
  useful, and no dynamic maps crossing feature boundaries.
- Keep mobile workflows resilient: cancellation after awaits, no setState after disposal, bounded retries, idempotent
  commands, safe duplicate-submission handling, and visible sync state.
- Preserve responsive behavior, RTL, accessibility, text scaling, and performance on representative phone/tablet widths.

## Security and reliability checks

- Tokens, secrets, connection strings, and sensitive ERP data never enter logs, screenshots, crash reports, analytics,
  query strings, or insecure local storage.
- Release builds do not use debug endpoints, permissive network security config, verbose logging, or weakened TLS checks.
- Deep links, push actions, cached objects, and offline restores re-check authentication, tenant, object existence, and
  permission before showing or mutating data.
- Money and stock values use project-approved value objects or typed primitives with explicit rounding, precision, unit,
  and currency rules.
- API errors distinguish validation, permission, network, stale data, and server faults without exposing internal details.

## Evidence

Require `dart format`, `dart analyze`, focused unit/widget tests, and integration or manual readback for critical flows
when available. For visible changes, require compact-phone and regular-phone checks, RTL/text-scaling notes when relevant,
and proof that loading, empty, error, permission, and success states are reachable or explicitly blocked.
