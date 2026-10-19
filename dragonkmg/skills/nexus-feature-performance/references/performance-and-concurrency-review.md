# Performance and concurrency review

Use only sections that match the changed path. Keep the test representative but bounded, and do not add a benchmark project unless repeated measurement will pay for its maintenance.

## Plan and complexity

- Trace the user request through API/UI, persistence, workers, events, integrations, and agent context only as far as the feature reaches.
- Name the primary operation and its expected input, throughput/concurrency, latency, memory, and correctness constraints. Use existing service objectives if available; otherwise record a baseline and avoid inventing an SLO.
- Identify algorithmic complexity and data cardinality. Avoid nested scans, N+1 queries, repeated serialization, unbounded history loads, and work proportional to all tenants when the operation is tenant-scoped.
- Keep complexity understandable: split responsibilities where that makes ownership and failure modes clearer. Do not refactor unrelated paths merely to lower a metric.

## Allocation and memory evidence

- Measure allocation rate/bytes per operation and retained-memory behavior on the target runtime with warmup and representative data. Compare before/after and repeat surprising results.
- Only claim zero-allocation when the measured method and full relevant call path are identified, the benchmark uses the intended runtime/configuration, and the input matrix covers cold/warm and boundary cases that could allocate. Report the tool and limits; do not generalize to the whole service.
- Check object lifetime, large object retention, duplicated transcripts, cache bounds/eviction, subscription disposal, and cancellation cleanup. Pool only when profiling identifies allocation pressure; prove ownership, reset completeness, and no cross-request/tenant data leakage.

## Locks, threads, and queues

- Map each mutable state owner and synchronization boundary. Prefer immutable messages and single-writer ownership over broad locks.
- Never use sync-over-async, block thread-pool workers for I/O, hold locks while awaiting, or create unbounded `Task.Run` fan-out. Bound concurrency and channel capacity; define backpressure and overload behavior.
- Set timeouts/deadlines, propagate cancellation, make writes idempotent where retried, and define graceful shutdown/drain semantics. Thread-safe does not mean race-free at the business transaction boundary.
- Test contended and cancellation/failure paths where relevant; collect wait/hold time, queue depth/age, active workers, retry count, and saturation evidence. Keep unrelated tenants isolated in both work and telemetry.

## Events and persisted context

- Keep event envelopes small and versioned. Put large content behind authorized references only when needed; avoid secrets and unnecessary personal/business data in durable events.
- Document producer transaction, outbox/inbox, delivery semantics, idempotency key, ordering key, retry limit, dead-letter handling, and schema evolution. Do not promise exactly-once effects from at-least-once transport.
- For conversations and agent handoffs, retain user-visible history separately from compact working summaries. Give every summary a source/session lineage and update point; retrieve original turns/artifacts when exact wording or state matters. Bound retention/compaction work and make recovery deterministic.

## Token and agent-team budget

- Prefer one agent for a bounded change. Add reviewers only when they can inspect independently and have non-overlapping write sets; run specialist review in parallel only after the interfaces/diff are stable.
- Assign roles by risk and changed surface (planner, implementer, reviewer/fixer, security, UX/performance, final monitor); do not make every role call a model for every task. Use deterministic local checks for repeatable validation when they are cheaper and adequate.
- Record model/provider/model ID, prompt/context inputs, token counts or cost when available, retries, and latency. If the provider omits usage, label it unavailable instead of estimating it as measured.
- Summaries should preserve goal, accepted plan, key constraints, decisions, file ownership, commands/results, and open risks. Keep bulky logs out of prompts; store concise evidence references and fetch exact artifacts when needed.

## Evidence record

For each claimed improvement, record:

| Field | Evidence |
|---|---|
| Path/workload | Operation, fixture size, tenant/concurrency shape |
| Environment | Commit, runtime, relevant configuration/hardware |
| Baseline → candidate | Same measurement units and method |
| Correctness | Tests and invariants checked alongside performance |
| Resource effects | Latency distribution, errors, allocations/memory, queries, queue/thread/token cost as relevant |
| Decision | Improved / unchanged / regressed / unverified, with caveats and rollback path |

Do not combine unlike workloads or claim percentages without the denominator. If a performance gate is not applicable, state why; if it could not run, mark it `BLOCKED` rather than passing it.
