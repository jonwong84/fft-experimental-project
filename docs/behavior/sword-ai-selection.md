# Hypothetical ordinary sword action selection

ZOD-140 implements the [ZOD-139 selection contract](../architecture/zod-139-ai-selection-contract.md):
one fresh movement/Attack composition, the original ranked lottery, then an
Attack or MoveOnly plan. Source: read-only fft_decomp
**11464943bbdbd2a0af022d90cb7f10455cf911df**.
[Implementation evidence](../evidence/ai/selection-implementation-v1.json).

## Inputs and source context

[`SwordAiSelection.Evaluate`](../../src/Zodiac.Framework/SwordAiSelection.cs)
takes the original snapshot, supplied composition scenario, weapon, existing
candidate/score/comparison/baseline/scan/composition profiles, a selection profile
and explicit decision RNG. It privately calls `SwordAiComposition.Evaluate` once;
there is no trusted cached-result or caller-populated ranked-list input.

The new profile requires three named booleans, all default false:

- `TerminalAttackEntry`: this Attack completes the source ability loop, with no
  more ability work before lottery. One evaluated Attack alone cannot prove that.
- `NoDefendSupport`: original support is not Defend. The bounded live snapshot
  has no support field; zero MP/equipment/IDs cannot establish this fact.
- `FreshOrdinarySelectionDefaults`: selected reset provenance and current/selected
  ChooseAtWait/reflected=false defaults match this fresh ordinary source context.

All are checked before composition work. Existing two-opponent, ordinary sword,
flat-ground, effect-free, stable equipment/team and original-state certificates
remain required. Scenario masks/destinations are still supplied and certified;
this does not generate them or establish an observed original-game decision.

## Lottery and selected plan

The selector starts at the fresh composition's progressed RNG. Each visited
ranked slot draws one checked BIOS output, including explicit empty/zero-hit
slots. The test is `raw % 100 < HitPercent`; equality fails. Diagnostics retain
slot, raw output, modulo, hit percentage and pass/fail.

In this zero/one-entry domain, first-roll success selects slot 0 in one draw.
First failure visits all eight slots and still selects the positive-hit slot 0.
An empty list visits eight slots then returns MoveOnly at **D0**, not D1. Fallback
adds no draw, scoring or tick work. Actual draw use is checked incrementally;
there is no reservation of eight draws before a successful one-draw selection.

`SwordAiSelectionOutcome` retains fresh composition diagnostics, lottery-entry
RNG, actual rolls, selected ranked index, reason and `SwordAiSelectedPlan`.
Reasons distinguish `FirstRoll`, `AllRollsFailedUseFirst` and `MovementFallback`.
Attack plans preserve the fresh candidate's actor, destination, target panel/
identity, order and score/projection diagnostics. A genuine zero or improving
negative priority remains a real Attack. MoveOnly has a null Attack payload;
the separate composition baseline is not invented as a selected Attack score.

`ActBeforeMove` reflects the selected Attack's order; it is false for MoveOnly.
`WaitFacingHint.ChooseAtWait` is a semantic deferred hint, not packed ordinal 5,
resolved facing or KeepCurrent. Selection publishes no command receipt or
decided=255 authority. It never applies predicted damage, movement, Wait, facing
or real choice consumption. Original snapshots/profiles/live RNG stay unchanged.

Failed new-profile admission, composition or lottery returns original whole-query
RNG and no partial composition/lottery/plan. Failure identifies the stage and
either the nested composition failure or failed lottery slot. Successful results
retain all real composition and visited lottery draws. The selection tail adds
no ticks and can finish at MAX ticks when composition itself needs no addition.
These transactions do not implement original C/VSync suspension or rewind.

## Validation and next work

The [27 focused C# cases](../../tests/Zodiac.Framework.Tests/SwordAiSelectionTests.cs)
cover SLC-001..007: literal first-roll/equality-failure streams, eight-slot empty
fallback, D0 versus D1, absent/equal/negative/zero priority, both Attack orders,
new source facts, nested failures, partial lottery rollback, exact-MAX draw use,
MAX ticks and deterministic input isolation/carry. They reuse composition rather
than duplicating its complete admission inventory. These are managed cases with
asserted source context, not original observations.

Validation: locked restore, zero-warning/error Release build,
**3,706 .NET cases / 496 Python tests**, including all 3,679 prior .NET cases.
The read-only source receipt verifies eight files / 31 functions. Authoritative
current .NET results are in ignored `scratch/ai-selection-140-final-trx/`;
build/test logs and source report use `scratch/ai-selection-140-*`.
No original/C execution or UI review. Standard
[build/test commands](../development/test-maintenance.md#commands) apply;
focused filter after building: `FullyQualifiedName~SwordAiSelectionTests`.

The bounded action-selection path now exists in C#. Next trace a manageable
original movement-scenario branch to derive S/D0/D1 from state, reducing supplied
inputs while preserving movement scoring/RNG order. General scenario/ability/
roster search, Defend, the replaceable decision interface, authoritative command/
RNG commitment and native/editor activation remain future work. Editor UI and
battle checkpoints ZOD-106 remain deferred.
