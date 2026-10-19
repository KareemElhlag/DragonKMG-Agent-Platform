---
name: nexus-feature-performance
description: Plan and review performance-sensitive NexusCore features when complexity, allocations, concurrency, memory, event flow, or agent context/token cost materially affect delivery. Use alongside KMG; not for routine edits without a measurable performance concern.
metadata:
  short-description: Performance engineering for NexusCore feature delivery
---

# NexusCore Feature Performance

Use with `$kmg-agent-skill` for lifecycle, architecture, and evidence gates. This specialist helps a feature team optimize the right bottleneck without weakening correctness, tenant isolation, auditability, or recoverability.

## Operating rules

- Start with a real workload and baseline. Separate API, database, UI, worker, memory, and model-token measurements; compare the same fixture, hardware/runtime, warmup, and concurrency. State uncertainty and do not turn an estimate into a measured result.
- Prefer reducing unnecessary work, queries, payloads, copies, and repeated context before adding concurrency or caches. Define correctness and consistency invariants before changing execution order.
- Treat cyclomatic complexity, allocations, locks, thread count, retained memory, queue depth, event volume, and context tokens as measurable budgets, not goals to minimize blindly. Zero allocation is a claim only for a bounded, benchmarked path and representative inputs; don't contort maintainable code or hide allocations behind pools.
- Keep mutable state ownership explicit. Use async I/O end-to-end; never block on async work or hold a monitor/lock across `await`. Bound parallelism, queues, retries, and fan-out; propagate cancellation/deadlines and expose backpressure. Add synchronization only for a demonstrated race and test concurrent access.
- Events are contracts, not a shortcut: use stable versioned schemas, minimal payloads, tenant/correlation/idempotency metadata as required, explicit ordering scope, and an outbox when atomic persistence-to-publish matters. Consumers must tolerate duplicate delivery and bounded retry/dead-letter behavior.
- Reduce model cost with scoped retrieval, concise task briefs, and summaries containing decisions, constraints, changed artifacts, and unresolved work. Retrieve source-of-truth files again before consequential decisions; a summary is navigation, not authority. Do not drop evidence, security context, or user choices to hit a token target.
- Keep all performance and correctness claims falsifiable. A green microbenchmark cannot prove end-to-end improvement; verify the affected workflow, error/timeout rates, and resource cost. Record unresolved measurements as `UNVERIFIED`/`BLOCKED`, with owner and next check.

## Use the focused reference

Read [performance-and-concurrency-review.md](references/performance-and-concurrency-review.md) when designing parallel workflows, changing event/session retention, making allocation claims, or preparing performance evidence. For a broad post-implementation readiness verdict, use `../nexus-master-audit-skill/SKILL.md`; for security depth use `$cyber-kmg-security` when available. Neither skill grants tools or authorizes external testing.
