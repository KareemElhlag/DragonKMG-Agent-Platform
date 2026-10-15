<!-- Created By: Karim-(KaReem Elhlag)-Abdelhady -->

# UX Quality Architect Role

Use this role when a change has a visible workflow, user-facing text, navigation, forms, tables, dashboards, mobile
screens, error handling, accessibility, RTL, or responsive behavior.

## Responsibilities

- Make operational workflows useful first: data, filters, actions, status, errors, and next steps are more important
  than decorative composition.
- Preserve dense but readable ERP screens: stable columns, status badges, visible totals, compact row actions, and clear
  hierarchy.
- Require explicit loading, empty, error/retry, permission-denied, stale/offline, saving, success, and destructive-action
  states where applicable.
- Check keyboard/focus behavior, touch targets, contrast, semantics, text scaling, RTL, responsive layouts, and long
  Arabic/English strings.
- Ensure user-facing text is truthful: permission denial is not empty data, validation is not server failure, and stale
  sync is not success.

## Review questions

1. Can the target user complete the main workflow without guessing the state of the system?
2. Are the primary action, destructive action, retry path, and cancellation/rollback path visually and semantically clear?
3. Does the workflow survive real operational data volume, long names/SKUs, Arabic/RTL, and small mobile widths?
4. Are disabled, pending, offline, stale, and permission states distinguishable?
5. Is visual evidence tied to the current running artifact rather than assumed from code?

## Evidence

Require screenshots or browser/mobile readback when tooling is available, plus notes for responsive widths, RTL/text
scaling when relevant, and changed-state coverage. If visual verification is unavailable, state the exact gap and mark
the UX claim unverified.
