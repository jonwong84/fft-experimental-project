# Selection from a freshly derived sword movement scenario

ZOD-145, 2026-10-28. Implements the [ZOD-144 contract](../architecture/zod-144-ai-derived-selection.md)
at reference **11464943bbdbd2a0af022d90cb7f10455cf911df**.
[Implementation receipt](../evidence/ai/derived-selection-implementation-v1.json).

`SwordAiDerivedSelection.Evaluate` accepts original two-opponent state, weapon,
candidate/score/comparison/scenario/derived/selection profiles and decision RNG.
It derives S/D0/D1, privately scores movement once, scans one terminal ordinary
Attack pass, applies strict baseline admission, then runs the original eight-slot
lottery and typed D0 movement fallback. Inputs include no masks, destinations,
scores, intermediate RNG checkpoints or cached outcomes.

`SwordAiDerivedSelectionProfile` contains known TargetActive and false-default
NormalScoring, CanonicalCoordinates, OrdinaryAttackTilePolicy,
TargetSelectionMatchesBaseline, FreshAbilityBestReset and FreshEmptyRankedList.
Candidate/score context, fresh comparison, scenario's focused/reach/policy facts,
and terminal Attack/no-Defend/fresh-selected defaults remain explicit. Since ZOD-149,
FirstAttackEntry (default false) is also required before movement: see the
[ability-prefix correction](../architecture/zod-148-ai-out-of-reach-selection.md).
The validation counts below describe the original ZOD-145 milestone. Mask and
fresh-cell provenance and the lack of intervening random scenario work are now
derived rather than asserted by the caller. These facts are still certificates,
not a general decision system sourced automatically from imported content.

The boundary remains raw Move<=124 and target distance<=Move+1 on ordinary clear
flat ground. Full target/ability/roster search, out-of-reach behavior, Defend,
special original policies, replacement interfaces and native/editor use are open.

## Fresh ownership and failure behavior

Pure provisional geometry/scan admission checks all weapon/state/policy facts
before scoring movement, including inactive targets. The scenario query then
derives its branch and evaluates each cell once from original state/ticks.
An internal composition continuation uses that invocation's actual immutable
movement entries and D0 baseline index. A shared internal selection tail uses its
fresh ranked result; neither tail is a public cached-result API. Legacy supplied
composition and selection APIs retain their existing signatures and behavior.

Success returns scenario and selection diagnostics. Its selection contains the
same fresh movement entries, not a recomputed prefix. Cost is actual |S| movement
draws + actual Attack draws + one/eight lottery draws. D0/D1 selection, baseline
lookup, insertion and fallback add no draws or ticks. Selected facing remains
ChooseAtWait. No live HP/CT/choices/RNG or commands are changed.

Admission, scenario, composition and lottery failures retain typed nested details
and return **original whole-query RNG** with no partial scenario, scores, rolls
or plan. Exact counter limits succeed when actual work fits; no eight-roll
reservation is made for a one-roll success.

## Source-derived checks

For a 5x3 map, actor(3,1), target(1,1), Move1, CT15/95, Speed5, both HP50,
PA5/power5/evade0, S contains (3,0),(2,1),(3,1),(4,1),(3,2).
D0=(2,1), D1=(3,2). Five movement draws finish count12 from word1/count7;
one MoveThenAct Attack finishes count17; the certain lottery ends count18.
All five movement entries are reused. The same result matches the existing
selector with independently stated S/D0/D1.

With TargetActive=false the same movement prefix reaches eight-slot MoveOnly
fallback at D0, finishing count20. With a zero-power active Attack, a real
equal-baseline score is rejected, then eight rolls finish count25. D1 is never
substituted for fallback D0. Adjacent already-moved partial-hit cases preserve
the original raw23010/modulo10 strict test: hit11 takes one lottery roll and
hit10 takes eight then the same slot-0 Attack, with no formula re-resolution.

16 focused cases cover these paths, all remaining facts, invalid weapon even
when inactive, unsupported scenario branch, whole-query late movement/Attack/
lottery rollback, exact draw/tick counters and both-identity/order replay.
Existing supplied-scenario regressions remain unchanged; 136 focused cases pass.

## Validation

Locked restore, zero-warning/error full Release build, **3,744 .NET / 496 Python**
tests passed. Full .NET: ignored `scratch/ai-derived-selection-145-full-trx/`;
focused: `scratch/ai-derived-selection-145-focused-trx/`; Python:
`scratch/ai-derived-selection-145-python.txt`; clean source audit:
`scratch/ai-derived-selection-145-source.json` (inherited selection receipt plus
11 inspected scenario functions). No original/C execution or UI review.
Owner definition/refactor drafts present in the working tree were included in
the solution build/tests and excluded from this milestone's commit.
