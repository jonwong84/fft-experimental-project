# Fresh out-of-reach pair selection with known cast scores — ZOD-202

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[ZOD-199 source/provenance study](../architecture/zod-199-pair-out-of-reach.md)
and [pinned receipt](../evidence/ai/pair-out-of-reach-contract-v1.json) cover
`scenarios_build`, `general_select`, `ai_cast_aim_tiles_build`,
`origin_reachable_evaluate`, `origin_next_take` and the inherited ranking/lottery.
This extends [scenario derivation](attack-throw-stone-ai-out-of-reach-scenarios.md)
without reconstructing retained source memory.

`AttackThrowStoneAiOutOfReachSelection.Evaluate` owns fresh both-unused NonBlue
preparation, full M0 proximity, filtered scoring, the post-score reach draw,
D0=D1, both action scans/ranking and one terminal lottery. It retains the same
normal-scoring, canonical-coordinate, no-other-overrides, clear-path and no-Defend
assertions as the [in-reach entry](attack-throw-stone-ai-prepared-selection.md).
The exact pair lists, refresh facts, no-special-behavior and clean-delay-cache
certificates still apply. No public cache, mask, destination or rank is an input.

## Why the scan mask may omit an unwritten actor

Source `ai_cast_aim_tiles_build` intersects the scenario with the non-arcing
range-plus-area diamond before `origin_next_take` reads any score key. Throw
Stone has range4/AoE0; ordinary Attack has range1/AoE0. In the admitted flat
two-opponent domain, an unscored scenario cell outside range4 therefore cannot
be a cast origin for either entry. Omitting that cell from the working scan mask
changes neither candidate order, scores nor RNG. The public result retains the
complete source Scenario1/Scenario2 masks and cell provenance separately from
ScanOrigins; no source mask or stored cell is rewritten to imply freshness.

If any unwritten Scenario1 cell is within range4, the entire selection rejects
with Provenance/UnwrittenCastPriority and incoming query RNG. This includes
original target distance4, even if another fresh tile could cast. Source ranked
origin selection compares every eligible key before choosing; a guessed zero or
assumption that the actor would lose is insufficient. Distance5 can be admitted.
Move0's no-match filter fallback is also safe: it freshly scores the actor, so
range3/4 ACT-first Throw Stone requires no unknown memory.

The private fresh scenario core now retains its already-computed prefix and
baseline index for an owning composition. Public scenario results are unchanged
and cannot be supplied back. The reach draw is already in the continuation RNG;
it is not repeated, moved before scoring or replaced by an extra actor evaluation.
The shared preparation-to-scan policy derivation is reused by both branches.
Legacy supplied-scenario scans still require their full scenario mask to be a
subset of fresh score cells. Their admission is not relaxed for this proof.

Failures after preparation, scoring, provenance checks, formula/clock work or
lottery return reasons only, no partial diagnostics/plan, and the original query
RNG. This managed atomic policy does not claim C rewind. Successful output is a
hypothetical plan with deferred facing; it grants no command authority, live RNG
adoption or presentation activation.

## Literal scenarios and validation

Actor(1,1), target(6,1), Move1, CT15/95, Speed5, PA5, HP50 and incoming `(1,7)`:
S contains only (2,1); the original actor has fresh proximity5 and unwritten
priority. It remains in both diagnostic scenarios but is excluded from ScanOrigins.
Attack has no candidates. Move-first Throw Stone from (2,1) scores26/hit100 and
enters rank0. Refresh costs one draw, movement one, reach adjustment one, Stone
five and lottery one. Movement ends `(2524885223,9)`, lottery starts
`(2633739833,15)`, raw16212/mod100=12 passes, and final RNG is `(3210001534,16)`.

With Move3, five scored cells select origin/destination(3,2). The final movement
checkpoint is `(2516284547,13)`; lottery starts `(836760821,19)`, raw9084 passes
and ends `(595337866,20)`. Seed20/Move1 halves reach and uses nearest fallback,
retaining the same single candidate and nine total draws. Move0/distance3/4
casts ACT-first at the actor. A KO candidate adds the second upkeep draw and
scores320. These are managed source-derived checks, not original-game recordings.

Distance6/Move1 has no cast origins and takes eight empty-slot lottery draws,
ending `(180171308,18)` with MoveOnly at D0. Zero accuracy still consumes four
formula draws, then eight empty-slot draws, ending `(2111915288,22)`. Seed20,
Move0, distance3 and evade75 exhaust all eight rolls and retain the first ranked
Stone, ending `(3660420676,23)`.

Six .NET scenarios cover these literals, both orderings, full costs and KO,
range4 rejection/range5 admission, masks/provenance, legacy admission, exact/short
budgets, late formula/tick failure and both-actor/order replay without live changes.
The first focused run exposed immutable-array identity equality in a test; the
assertion now compares contents, with no runtime correction required.
Locked restore, zero-warning/error Release rebuild, **4,054 .NET cases** across
seven projects (867 AI), **529 Python tests** and the pinned source/prepared-data
audit pass. Reports and seven full TRX files are under ignored
`scratch/ai-pair-out-of-reach-202/`. Owner content drafts participate in validation
and remain excluded from this commit. No original/C execution or UI review;
frame/VSync stream equivalence remains unresolved.

## Next boundary

ZOD-203 should trace and design the authoritative Throw Stone command handoff.
Distinguish the AI's midpoint/no-knockback projection from actual execution
accuracy, damage, knockback and RNG; bind plans to fresh Framework-owned planning
transactions and actual movement/facing receipts. Select one bounded implementation
child before coding. Keep unknown-cell admission, generic list/status expansion,
replacement interfaces and host/editor activation outside that design.
