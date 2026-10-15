# Conversation latency profiles — 2026-10-15

## Observed baseline

Existing Security Test Agent/openrouter/free run: input12characters, effectiveInstructions42,783characters, run duration11.1386seconds. This run timing includes provider completion and run writes, not separately measured network/queue/inference phases.

## Changes

- Exact, short allowlisted greetings in sessions are answered locally and explicitly labelled “local / no model call.” Unknown messages, technical content and greeting-plus-instructions still use the provider.
- The session API and terminal preserve agent enabled, role/mode/tool grants, connection enabled/cloud egress, disabled model and secret resolution checks before accepting a local reply.
- Local replies persist a source marker in run evidence and normal user/assistant session messages. They are not passed off as model-generated text.
- CLI /quick <text> explicitly selects brief conversation without specialist skill bodies. API callers can set quickChat:true; defaultfalse.
- Quick retains core authorization guardrail, agent instructions, role instructions and mode restrictions. It adds a no-implementation/no-tools/no-test-claims instruction and caps output at384tokens.
- Ordinary technical prompts and council execution retain full assigned skills; no automatic task classifier silently drops custom skill instructions.
- CLI completion shows total elapsed time and effective instruction character count. API response exposes elapsedMs, instructionCharacters and source (local/provider).
- Status text no longer pretends that security tests are executing while waiting for a model.

## Live evidence

Same configured agent/connection after deployment:
- Local “مرحبا اختبار”: source local, server elapsed162ms, effective instruction chars820. Cold HTTP client wall time1576ms separately; not conflated with server measurement.
- Actual quick provider request “رد بكلمة واحدة فقط: جاهز”: response “جاهز”, elapsed3693ms, instruction chars768.
These are different prompts and execution paths, so this is not a controlled model-speedup percentage, percentile or provider latency guarantee.

Verification:73/73 .NET tests; solution0warnings0errors. Tests cover explicit local labeling/no gateway call, technical input exclusion, quick/full skill-body selection, preserved guards/output budget, and disabled-connection rejection.
Independent review: CERTIFIED_WITH_RESIDUE. No paid model was added or selected.

Remaining: provider/network/queue phase breakdown and token streaming are not implemented. Free-router availability and latency vary. Quick profile is intentionally not a substitute for full specialist implementation/review.
Rollback: restore previous CLI/session orchestration and PlatformService optional profile parameters; no schema rollback or session deletion required.
