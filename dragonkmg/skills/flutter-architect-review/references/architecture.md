# Flutter/Dart Architecture Reference

Use this reference when changing or reviewing Flutter architecture, package structure, state management, routing,
dependency injection, networking, caching, offline behavior, or domain boundaries.

## Baseline Shape

The default production shape is feature-first and layered:

```text
lib/
  app/
    app.dart
    router.dart
    dependency_graph.dart
    theme/
    localization/
  core/
    errors/
    money/
    networking/
    permissions/
    result/
  features/
    inventory_transfer/
      presentation/
        transfer_list_screen.dart
        transfer_form_screen.dart
        widgets/
      application/
        transfer_list_view_model.dart
        submit_transfer_command.dart
      domain/
        transfer.dart
        transfer_line.dart
        warehouse_id.dart
        transfer_policy.dart
      data/
        transfer_repository.dart
        transfer_api_client.dart
        transfer_dto.dart
```

Adapt names to the repository's conventions. Do not impose this exact tree when the app already has a coherent architecture.

## Dependency Direction

- Presentation depends on application state/view models and UI components.
- Application depends on domain contracts and repository interfaces.
- Domain depends on no Flutter package and no infrastructure package.
- Data implements repositories and maps external DTOs into domain/application models.
- Core contains truly shared primitives only: errors, result types, networking, money, auth/tenant context, logging,
  permission abstractions, and design-system utilities.

Domain code should be testable with plain Dart tests. If a domain object imports Flutter widgets, navigation, storage,
or HTTP clients, the boundary is leaking.

## MVVM, Use Cases, And State

Flutter's official architecture guidance recommends splitting apps into UI and data layers with views, view models,
repositories, and services; complex apps can add a domain/use-case layer. Use that as the baseline.

- View: renders UI state and forwards user intent.
- View model/controller/notifier: owns screen state, commands, validation orchestration, and conversion from domain/data
  models into UI-ready state.
- Use case/interactor: owns multi-step business flow when the logic is meaningful outside one screen.
- Repository: source of truth for data access from the feature's point of view.
- Service/API client: talks to external systems, local storage, platform plugins, or background services.

Use case classes are worth adding when logic is reused, transaction-like, security-sensitive, hard to test through a
view model, or part of ERP business rules. Avoid use-case ceremony for simple pass-through reads.

## State Management Choice

Do not pick a state package by fashion. Choose by repository precedent and operational need:

- `Riverpod`: strong default for testable dependency injection, feature isolation, async state, and large apps.
- `Bloc/Cubit`: good when the team already models workflows as events/states or needs strict transition visibility.
- `Provider`/`ChangeNotifier`: acceptable for simpler apps or existing codebases; keep mutation disciplined.
- Local `StatefulWidget`: only for ephemeral UI state such as text field focus, transient expansion, animation, tab index,
  or one-off local toggles.

State objects should be immutable. Prefer explicit unions/sealed states or typed result wrappers over nullable field
combinations that allow impossible states.

## ERP And NexusCore Rules

- Preserve tenant, branch, warehouse, fiscal period, and permission context through every request and cached record.
- Money must use a value object with currency, amount, rounding policy, and formatting separated from arithmetic.
- Inventory transfer flows must expose draft, submitted, approved, posted, rejected, cancelled, and sync/error states
  according to the domain model, not just generic success/failure.
- Avoid client-side authority for ledger, stock, or entitlement decisions. The app may guide, validate, and explain, but
  the server remains authoritative for posting, stock movements, COGS, journals, and permissions.
- Offline support requires explicit conflict policy, idempotency keys, retry states, and visible sync status. Do not imply
  offline safety from local caching alone.
- **Offline-queue durability vs. logout must be a named, deliberate decision, not a default.** A durable local
  queue (fsynced, encrypted-at-rest) that is also wiped on logout silently discards a user's genuinely unsynced
  work the moment they sign out on a shared device — the same durability that protects against a process kill
  works against the user if logout clears it unconditionally. Decide explicitly per queue: (a) block/warn on
  logout while pending items exist and require the user to either sync or explicitly discard, or (b) keep the
  encrypted queue keyed by account and resume it transparently on the next login by the same account, never by a
  different account on the same device. Silently wiping on every logout and silently keeping it forever are both
  defaults to avoid; state which decision was made and why in the review.
- A durable local queue's `clientRequestId` must be generated once per logical operation and persisted with the
  queued item itself, never regenerated on retry/resend — regenerating it defeats the idempotency guarantee the
  server-side unique index depends on.

## Package And Dependency Rules

- Keep `pubspec.yaml` lean. Add dependencies only for clear value and active maintenance.
- Prefer generated immutable models only if the project already supports the build pipeline or the gain is substantial.
- Use typed API clients and DTO mapping. Do not let backend JSON maps spread into presentation.
- Keep platform-channel code isolated behind interfaces and test the fallback/error behavior.
- Avoid service locators inside domain and view code. If a locator exists, confine it to composition roots or legacy seams.

## Routing

Use typed routes or centralized route definitions when possible. Protect routes with auth, tenant, branch, and permission
checks before expensive loading. Deep links must validate identity, tenant, object existence, and permission after restore.

## References

- Flutter app architecture: https://docs.flutter.dev/app-architecture/guide
- Flutter state management: https://docs.flutter.dev/data-and-backend/state-mgmt
- Flutter architectural overview: https://docs.flutter.dev/resources/architectural-overview
- Effective Dart: https://dart.dev/effective-dart
