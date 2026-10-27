# Movement scoring and ordinary Attack ranked admission

ZOD-138 implements the [ZOD-137 composition contract](../architecture/zod-137-ai-composition-contract.md)
as an opt-in hypothetical query. Source: read-only fft_decomp
**11464943bbdbd2a0af022d90cb7f10455cf911df**. It evaluates the supplied movement
scoring mask, reuses its fresh baseline and scans ordinary Attack before ranked
admission. It stops before lottery, fallback or command choice.
[Implementation evidence](../evidence/ai/composition-implementation-v1.json).

## Inputs and ownership

[`SwordAiComposition.Evaluate`](../../src/Zodiac.Framework/SwordAiComposition.cs)
takes original `AttackBattleSnapshot`, `SwordAiCompositionScenario`, weapon,
existing candidate/score/comparison/baseline/scan profiles, the new composition
profile and explicit decision RNG at the start of movement scoring.

The scenario copies pre-scoring origins S and destinations D0 (baseline) and D1
(Attack). S must be nonempty, unique and a subset of original `AiOrigins`; both
destinations must belong to S. The query derives Attack mask A=S union actor.
It does not accept scores, cached outcomes, intermediate RNG checkpoints or
pre-populated ranked entries. Masks/destinations are certified inputs; this API
does not generate them or prove they came from an original-game decision.

The new profile has three named booleans, all default false:
`OriginalMovementScoreMask`, `NoInterveningRandomWork`, `FreshEmptyRankedList`.
Existing baseline and scan certificates still apply, including required known
TargetActive and fresh best/cache. A no-intervening-work assertion excludes the
original non-player out-of-reach reach roll and other intervening evaluations.
Reachability or IDs cannot establish these unrepresented facts.

The domain remains two living, positive-speed ordinary opponents on flat dry
ground, with unchanged equipment/teams, zero MP/Golem, no charges, pickups or
upkeep/terrain effects. Original action, geometry, weapon/envelope and all profile
checks run before any movement evaluation, including inactive/no-origin Attacks.

## Evaluation and results

Every S tile is evaluated once in layer/y/x order from original state and original
tick count. Only decision RNG is carried. Each completed movement evaluation
consumes one upkeep draw; CT refunds and first readiness remain real computations.
The already-evaluated D0 cell supplies the baseline, adding zero draws. The shared
internal movement evaluator does not assert that every S tile is D0. Standalone
baseline behavior remains unchanged.

The existing scan runs once on original state using A/D1 and the progressed RNG.
It keeps separate evaluations for duplicate destinations, fresh pre-KO proximity,
earliest exact ties, and all losing/NotValuable draws. Standalone scan behavior
remains unchanged. New internal admission helpers perform no candidate evaluation.

Success returns immutable movement results/RNG checkpoints, baseline index/reference,
derived Attack scenario, fresh scan diagnostics, admission and eight ranked slots
with explicit null empties. Admission is `NoScoredCandidate`, `NotAboveBaseline`
or `Admitted`. A real zero or negative score is not absence. Candidates must
strictly beat baseline priority; better proximity cannot rescue equality.
Insertion compares the tail/first outranked entry, shifts full immutable payloads,
does not deduplicate, and discards the private working best after the attempt.

This fresh-empty-list, one-pass subset admits **zero or one real entry at slot 0**.
Generic capacity/tail/duplicate kernels remain source-only beyond that reachable
C# coverage. Ranked entries grant no command authority. An empty list does not
choose movement, and an admitted entry does not execute the later lottery.

Failure returns original query RNG, no partial outcome and a typed reason/stage
(`Admission`, `Movement`, `Attack`), with source-order evaluation index and nested
rejection where applicable. Live snapshots/RNG never change. This atomic failure
policy is managed behavior, not native VSync resume or original C rewind.

## Validation and next boundary

The [46 focused C# cases](../../tests/Zodiac.Framework.Tests/SwordAiCompositionTests.cs)
execute CMP-001..008: prefix/Attack RNG order and baseline reuse, actor union/stay,
non-attackable movement cells, inactive/NotValuable scans, equal/negative/zero
admission, each required certificate, invalid state/destinations, partial-prefix
exhaustion, MAX clocks/draws, original-state KO cache and deterministic isolation.
These are managed cases with asserted source context, not original observations.
Existing standalone regressions also pass; no broad duplicate fixture inventory
or private-reflection tests were added.

Validation: locked restore; zero-warning/error Release build; **3,679 .NET cases /
491 Python tests**. Source audit verifies four files / 27 functions. Logs/TRX/report
are ignored under `scratch/ai-composition-138-*`. No original/C execution or UI check.
Use standard [build/test commands](../development/test-maintenance.md#commands);
focused filter after building: `FullyQualifiedName~SwordAiCompositionTests`.

[ZOD-140 selection](sword-ai-selection.md) now privately owns this composition,
the source lottery and no-Defend movement fallback. This standalone query still
stops at ranked admission. Full scenario/ability search, replaceable decision
interface, authoritative commands/RNG and native/editor activation remain future
work. Editor UI and ZOD-106 remain deferred.
