# AI assembly and authoritative command handoff

ZOD-157/158, 2026-10-28. Behavior-preserving extraction from managed base
`dec35ba`; original behavior evidence remains pinned to read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`. This work changes ownership and command
publication, not original search policy or the unresolved refresh/frame RNG scope.

`Zodiac.Framework.Ai` references `Zodiac.Framework`, which references
`Tessera.Engine`. Framework has no AI reference. AI simulation, scoring, ranking,
selection and execution orchestration move together with their tests. Shared
movement geometry, actual combat/turn rules, RNG primitives, session construction
and completion receipts remain in Framework. Existing type names stay unchanged;
AI namespaces become `Zodiac.Framework.Ai` / `.Tests`.

## Controlled planning transaction

Framework invokes a synchronous planner once after checking the active actor,
pending facing and revision. A new context exposes that exact immutable battle,
actor and incoming RNG. Only that context can prepare an opaque Attack, Wait or
rejection. Context preparation closes when the callback returns or throws.

The returned decision must belong to this invocation. A cached decision from an
older invocation rejects even on an identical immutable input. Callers cannot
construct decisions, trusted sessions or completion receipts. This guarantees
proposal binding, not that a policy's score or diagnostics are truthful: choosing
the policy is the application's responsibility. AI retains its private fresh
selection call and accepts no public cached-result executor input.

Before adoption, Framework verifies that the proposed RNG is a forward checkpoint
of its BIOS generator, including both word and diagnostic count. A wrong word or
decreasing count rejects. Verification uses composition of the existing UInt32
LCG in O(log draw-count), so an enormous claimed count cannot cause an enormous
validation loop. This validates continuity, not whether the policy needed every
reported draw. A replacement policy may intentionally consume a different count.

Only then does Framework stage RNG into both snapshot copies, retaining profile,
lineage and revision, and delegate the actual command to existing rules. Actor
identity is rechecked. Decision/adoption adds no revision or clock event; an
accepted command retains the existing single revision increment and fully
settled receipt publication. Rejected operations publish the original session,
empty events and no success diagnostics, including late command failure.

AI's existing context admission and reason-only failure mapping stay in AI.
For fallback Wait, AI resolves fresh facing against the same settled snapshot;
Framework applies the fixed facing through its ordinary Wait command. Standalone
AI Wait/receipt adapters retain their behavior. Read-only validation queries may
be public; their results do not grant command authority.

## Verification and scope

Keep existing decision/RNG/scoring vectors and integration tests. Add focused
Framework coverage for stale/closed proposals, invalid RNG progress, actual late
failure rollback, fresh invocation and large-count verification. Keep test-only
friend access for counter/receipt invariant cases; grant no production friend
access to AI. Dependency validation must recognize the new project without
weakening Engine independence or permitting a Framework-to-AI reference.

No complete command bus, AI plugin interface, saveable decision token, native
activation, original refresh prefix or broad replacement contract is introduced.
The existing concrete planner provides the consumer needed for this boundary.
ZOD-156 remains the next original-behavior investigation after extraction.

The later [ZOD-183 post-ACT design](zod-183-post-act-command-boundary.md) proposes
a narrow turn-ending path Move extension to this same transaction, with actual
receipt/facing settlement. That entry is not implemented; ZOD-184 owns it.

## Implementation validation

Fourteen source files and 15 existing test files moved into the AI projects.
All 673 existing AI cases retain their expectations; one dependency check was
added. Seven Framework cases exercise the new transaction and RNG checkpoint
verification. Source formulas/scoring/search order and packed original data are
unchanged. The original gameplay prototype still uses its existing host path.

Locked restore and zero-warning/error Release build passed. All **3,845 .NET /
500 Python tests** and **nine historical checkpoint audits** passed. The count
includes the owner's pending content/settings refactor, which remains excluded
from this work's commit. Current paths, provenance and ignored result reports:
[implementation evidence](../evidence/ai/module-extraction-v1.json).
No C/original execution, UI review or host activation was performed.
