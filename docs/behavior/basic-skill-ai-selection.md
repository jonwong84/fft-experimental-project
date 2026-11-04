# Three-entry Basic Skill AI — ZOD-234–236

Implemented 2026-11-04 from managed base `4aa1314`, following the
[ZOD-233 source contract](../architecture/zod-233-three-entry-ai.md). Read-only
fft_decomp HEAD was rechecked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. This is the closed Attack, Accumulate,
Throw Stone list; the type name does not imply the entire Basic Skill catalog.
The [implementation receipt](../evidence/ai/three-entry-implementation-v1.json)
records validation and the independent literal stream.

## Shared entry and rank owners — ZOD-234

`AiRankedInsertion` now owns the existing private pair insertion operation.
It requires a strict rank improvement over the move baseline, then uses
priority/hit/proximity to insert, preserving earlier entries on complete ties.
The old pair pipeline uses the same helper with unchanged public APIs and draws.
No public caller can provide trusted ranked slots.

`AccumulateAiEntryEvaluation` owns one source-order usefulness filter and its
ACT-first/distinct-destination Move-first self candidates. It accepts original
state, explicit profiles and the continuing private RNG. It does not refresh,
score movement or accept a cached usefulness/projection result. The prior
isolated `AccumulateAiMapEntry` delegates to it and preserves its result shape,
streams, admission and whole-query rollback. All 952 prior AI cases passed
after this extraction, before adding the three-entry pipeline.

## Fresh supplied-scenario composition — ZOD-235

`BasicSkillAiComposition` privately owns:

1. Explicit general-entry/order/fresh-rank assertions and fresh three-action
   preparation through `AccumulateAiPreparation`, including one refresh draw.
2. Admission of the immutable supplied S/action masks/D0/D1 through existing
   Attack/Stone scan validators. Both active opponents must match the fresh
   general-search preparation. D0/D1 must have known scored cells; no zero score
   is invented for an inserted unscored actor tile.
3. One `AiMovementPrefix` in source layer/y/x order, including the single
   baseline and proximity cache reused by every entry.
4. Attack scan and insertion, Accumulate filter/candidates and insertion, then
   Throw Stone scan and insertion. Each works from unchanged actual units.
   Only decision RNG, the private movement cache and the ranked array cross
   entries. The Accumulate coin consumes Attack's retained stream; its PA boost
   never changes the later Throw Stone projection.

Typed diagnostic actions preserve effect identity: actor/opponent Attack,
actor/self Accumulate with an independent hostile scoring ID, and actor/opponent
Throw Stone. Eight ranked slots preserve original compatibility behavior.
Insertion-index diagnostics describe the index at insertion time; a later
entry can shift earlier entries. Ranked output reflects final order.

Both admitted NonBlue and Blue profiles reuse the same pipeline. Blue retains
its explicit AI-control and HP threshold admission; policy differences remain
in fresh preparation. This supplied-scenario query does not derive S/D0/D1 or
add the out-of-reach team adjustment draw. The explicit source-scenario boundary
and other unrepresented facts remain certificates. An empty damage mask does
not suppress Accumulate's direct D1 evaluation.

## Terminal lottery and typed plan — ZOD-236

`BasicSkillAiSelection` owns a fresh composition, then calls `AiRankedLottery`.
Terminal-list, ordinary defaults and no-Defend assertions are required. First
successful visited slot wins; eight failures with a nonzero hit use slot0;
eight zero-hit slots produce MoveOnly at D0. The output distinguishes Attack,
Accumulate, ThrowStone and MoveOnly, with semantic ChooseFacingAtWait retained.
There is no public cached-rank/plan input, live RNG/choice commitment, trusted
Framework receipt or self-command dispatch.

Every failure, including a later candidate or the final lottery, publishes no
partial output and restores the incoming whole-query RNG. Successful rejected-
by-ranking and NotValuable candidates retain their source draws. Public
diagnostics are never treated as executable state or authority.

## Validation and limits

The source contract's word1/count7 case with class evade97 produces final ranks
Attack(26,3), Stone(26,3), Accumulate(3,100). Draw9=16212 is the self coin;
lottery raws25089/21183/25137 select slot2, ending at word3794901700/count26.
Tests separately cover first-slot Attack, ranged Stone, all-failed-use-first,
eight-slot empty fallback, empty damage masks, one/two self origins, retained
unboosted PA/HP across entries, exact and insufficient budgets at distinct stages,
both teams/actor identities, collection-order replay and unknown-score rejection.

Full validation results and reports are in the handoff and implementation receipt.
Locked restore and the zero-warning/error full Release build pass, together with
**4,169 .NET cases** (965 AI, including 13 new cases), **539 Python tests** and
pinned source/prepared-data and algorithm-guide audits. Seven full TRX files,
logs and preservation receipts are under ignored `scratch/basic-skill-ai-233-236/`.
Owner drafts were included in validation and remain excluded from this change.
Unchanged historical checkpoint audits were not rerun.
Reproduce with the pinned SDK and Python/Pillow setup documented in the
[Accumulate implementation](accumulate-ai.md#validation):

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
python -m unittest discover -s tools/verification -p 'test_*.py'
python tools/verification/verify_ai_algorithm_guide.py --reference C:/repositories/fft_decomp
python tools/verification/verify_ai_accumulate_contract.py --reference C:/repositories/fft_decomp --prepared scratch/all-data-extraction/SCUS_942.21
```

The [ZOD-237–239 self-action handoff](immediate-self-act.md) now executes fresh
ACT-first selections through Framework-owned actual rules. Derived three-entry
scenarios, Move-first/post-ACT execution and full original policy/roster/map
breadth remain open. Host/editor activation, battle saves and
original-frame RNG equivalence remain deferred/unresolved; boosted PA stays
battle-only. No C/original execution or presentation review was performed.
