# Fresh out-of-reach pair scenarios — ZOD-200

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[ZOD-199 source/provenance study](../architecture/zod-199-pair-out-of-reach.md)
and its [pinned receipt](../evidence/ai/pair-out-of-reach-contract-v1.json).

`AttackThrowStoneAiOutOfReachScenario.Evaluate` owns fresh both-unused NonBlue
pair preparation. Restored-state target distance must exceed minimum enemy
reach Move+1; Throw Stone's range4 does not change that branch condition.
Normal scoring, canonical coordinates and no other scenario overrides remain
explicit assertions. The ordinary flat two-opponent domain, raw Move<=124 and
preparation admission remain unchanged.

The shared internal movement prefix now optionally derives proximity over the
full original reachable mask M0 while evaluating only the filtered score mask S.
It uses original CT/positions before any private movement clock. Existing callers
retain their original domain by default. Here S includes M0 cells at target
distance <= original distance-1; if none qualifies, S falls back to M0.
Each S cell is scored once in layer/y/x order. No extra actor score is added.

After scoring, NonBlue consumes one BIOS RNG draw. A raw value divisible by seven
halves reach with integer division. D1 uses signed negated priority/proximity,
then distance, then later coordinates within effective reach. If no cell is in
range, the unrestricted distance-first fallback runs instead. D0 copies D1;
the in-reach ranged D0 chooser is skipped. BaselineIndex identifies that already
scored cell without rescoring it.

Scenario2 is M0 minus S, computed before final Scenario1 becomes S union {actor}.
The masks may therefore overlap at the actor. Immutable cell diagnostics cover
all M0: every cell has fresh proximity, but WrittenPriority is nullable and
PriorityWritten is false outside S. Null means no current-decision write; it
does not mean zero or establish retained source contents. Shared event/status
aliasing and initial-memory uncertainty from ZOD-199 remain unresolved. These
diagnostics are distinct from scan scenarios that require fresh priorities.

This query stops before action scans/ranking/lottery and produces no plan,
command authority or live RNG adoption. It accepts no cached results or prior
memory. Unsupported input or exhausted private clocks/draw counters publish
reasons only and restore the incoming whole-query RNG, including after refresh,
movement or the final reach draw. This is managed atomic policy, not C rewind.

## Literal checks and validation

Actor(1,1), target(4,1), Move1, CT15/95, Speed5, HP50 and incoming RNG `(1,7)`:
M0 has five cells and S only (2,1). Final Scenario1 includes the actor and (2,1);
Scenario2 still includes the actor. Actor proximity is 7 with no written priority;
(2,1) has proximity 8 and priority 0. Refresh draws 16838, movement draws 5758 and
reach adjustment draws 10113/mod7=5. D0=D1=(2,1), effective reach 2; final RNG is
`(662824084,10)`. These include refresh, unlike ZOD-199's post-preparation examples.
Original target distances4/5 preserve the same unwritten cell and draw count;
the scenario-only entry accepts both without attempting a cast or key read.

Seed 20 produces refresh 9086, movement 2192 and adjustment 12502/mod7=0. Reach 2
becomes 1, requiring nearest fallback. Move0 instead triggers the no-match mask
fallback and freshly scores the actor; a halved reach of zero remains supported.
Move3/target(6,1) scores five cells, selects D0=D1=(3,2), and ends RNG
`(3655513600,14)`. When the target is not imminent, all original proximities are
zero and distance ordering selects (4,1) instead.

Six .NET scenarios cover those literals/masks/provenance, halving and fallback,
exact/short draw budgets, late tick failure, original-delay ordering, legacy
fresh-cell admission and both-actor/order replay without live changes.
Locked restore, zero-warning/error Release rebuild, **4,048 .NET cases** across
seven projects (861 AI), **529 Python tests** and the pinned source/prepared-data
audit pass. Logs and seven full TRX files are under ignored
`scratch/ai-pair-out-of-reach-200/`. The first Python run encountered sandbox temp
permissions; rerunning with normal temp access passed (`python-final.txt`).
Owner content drafts participate in validation and remain excluded from this
commit. No original/C execution or UI review; frame/VSync RNG equivalence remains open.

## Next boundary

ZOD-202 can compose fresh out-of-reach selection only when every actual cast key
is freshly written or proved unread. Reject an unwritten original actor that
can still cast Throw Stone; do not infer zero, rescore it or accept cached memory.
Prove range filtering before adapting masks for existing fresh-cell scans, retain
the full diagnostic source masks, and reuse one owned prefix without extra draws.
General cache-history modeling, other lists/statuses, blocked trajectories, live
pair authority, replacement interfaces and presentation activation remain separate.
