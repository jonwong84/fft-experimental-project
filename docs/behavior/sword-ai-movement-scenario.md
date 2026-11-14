# Fresh in-reach sword AI movement scenario

ZOD-143, 2026-10-28. Implements the [ZOD-141 contract](../architecture/zod-141-ai-movement-scenario.md)
at reference **11464943bbdbd2a0af022d90cb7f10455cf911df**.
[Implementation receipt](../evidence/ai/movement-scenario-implementation-v1.json).

`SwordAiMovementScenario.Evaluate` derives the pre-scoring mask S and D0/D1 for
the focused, in-reach ordinary-sword general-search branch. Inputs are original
state, candidate/score/comparison profiles, a new three-fact scenario profile and
explicit decision RNG. No supplied masks, destinations or score grids are accepted.

False-default facts certify focused opponent, ordinary-sword reach summary and
ordinary policy. Fresh comparison context and existing two-unit geometry/scoring
certificates still apply. Raw Move must be <=124; target distance must be <=Move+1.
Stay-near, ranged/reflected/crystal overrides, out-of-reach random work and special
branches remain excluded. These caller assertions are not original-data-derived proof.

The query derives target-distance rows and S from existing flat geometry. S=R in
this branch, with the actor already present. Every cell is freshly evaluated once
in layer/y/x, retaining actual RNG and original per-evaluation ticks. Shared
baseline admission was extracted without changing the standalone baseline API.

The result records immutable movement evaluations, target distances, original
proximity, signed packed comparison values, D0/D1, baseline index and whether D0
needed distance-first fallback. Positive/negative priorities retain u16 packing
followed by signed s32 comparison. Complete destination ties keep the last tile.
No additional draw is used for either destination or baseline lookup.

For a 5x3 map, actor (2,1), target (1,1), Move=1, actor CT15 and target CT95,
Speed=5, S is (2,0),(2,1),(3,1),(2,2). D1=(2,2) by last equal proximity/distance;
D0=(2,1) by adjacency fallback. Starting RNG word1/count7 ends word3295386429/
count11. If target CT=0, proximity is zero and D1 instead stays at (2,1). Original
ticks20 yield 33/29/33/33 per tile in that case, never cumulatively advancing
the next evaluation's start tick.

Failures publish no partial evaluations/scenario and restore whole-query input
RNG. Typed stage/index distinguishes admission and later tile rejection. This is
a synchronous managed transaction, not original C rewind/resume. Original state,
live CT/RNG and choices remain unchanged. No Attack, lottery, Defend, commands,
facing or native/editor activation occurs.

## Validation

22 focused source-derived managed cases cover branch boundaries, proximity and
last ties, D0 fallback, singleton/already-moved semantics, signed packing, source
certificates/admission, late draw/tick rollback, exact counters and replay/order.
Existing baseline/composition/selection regressions also pass (120 focused total).
Initial compile/fixture clock expectation errors were corrected before the final
build and full run; initial failed TRX is not completion evidence.

Locked restore, zero-warning/error Release build, **3,728 .NET / 496 Python**
tests passed. Full results: ignored `scratch/ai-scenario-143-full-trx/`; focused:
`scratch/ai-scenario-143-final-focused-trx/`; Python log:
`scratch/ai-scenario-143-python.txt`. No C/original execution or UI review.
The solution run includes the owner's present definition drafts/refactor; those
files are preserved and excluded from this milestone's commit.
