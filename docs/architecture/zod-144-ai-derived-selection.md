# ZOD-144 — Private derived-scenario selection continuation

2026-10-28, source/design. Read-only clean reference
**11464943bbdbd2a0af022d90cb7f10455cf911df** rechecked through the
[scenario receipt](../evidence/ai/movement-scenario-contract-v1.json), including
the inherited composition/selection source receipt. Report:
`scratch/ai-derived-selection-144/source.json`. No production change or C/original
execution in this checkpoint. [Evidence](../evidence/ai/derived-selection-contract-v1.json).

`general_select` orders `scenarios_build`, D0 baseline lookup, no-charge early
return check, active-target selection, completed ability loop, ranked lottery,
then D0 fallback if empty. ZOD-143 now owns the first stage for the in-reach
focused ordinary-sword branch. ZOD-145 will connect its actual private results to
the existing single terminal Attack pass and lottery. Full original AI is still
not implied by this slice.

## Inputs and derived facts

Add one opt-in pure derived-selection query taking original state, weapon,
candidate/score/comparison/scenario/selection profiles and explicit RNG. It accepts
no masks, D0/D1, score arrays, public scenario results or ranked outcomes.

Use a new semantic profile for the remaining facts: known TargetActive and
false-default NormalScoring, CanonicalCoordinates, OrdinaryAttackTilePolicy,
TargetSelectionMatchesBaseline, FreshAbilityBestReset, FreshEmptyRankedList.
Keep the scenario profile's three facts, fresh comparison context and terminal
Attack/no-Defend/fresh-selected-default selection facts. Original IDs are not
proof of these unrepresented properties.

The new query derives original S/A provenance, D0/D1 and fresh baseline-cell
provenance structurally. The in-reach branch has no intervening random work.
Do not request caller certificates for these now-derived facts; do not remove
them from legacy supplied-scenario APIs. Normal scoring and canonical source
coordinates remain explicit context certificates, not inferred from a successful
geometry query. Replacement/general decision interfaces stay deferred.

## Preflight and fresh ownership

Before movement simulation, validate all new facts and the existing selection
tail facts. Use a pure original-geometry/provisional-scan admission check to
validate weapon identity/envelope, candidate/score profiles, state, and ordinary
Attack policy even when TargetActive=false. This provisional scenario is not an
evaluated Attack and adds no draws/ticks. Scenario branch/reach admission also
precedes its first tile simulation.

1. Privately call the fresh movement-scenario query once.
2. Build A=S union actor after movement; pass D1 to the Attack scan.
3. Continue composition with that invocation's private immutable movement
   evaluations and its D0 baseline index, at the movement-end RNG. Read D0's
   computed score; do not call old `Composition.Evaluate` or rescore any cell.
4. Run one existing fresh Attack scan and strict ranked admission. No authority
   comes from a public cached object. Preserve full candidate diagnostics,
   negative/zero priority and original pre-KO proximity.
5. Run the existing source lottery on this privately produced composition. The
   selected movement fallback uses derived D0, never D1. Retain deferred facing
   and the absence of an Attack payload on MoveOnly.

Extract only internal continuation helpers from existing composition/selection
methods so supplied and derived entry points share the proven tails. Internal
helpers are called only with fresh results owned by those entry points. No
public cached-result executor, assembly/plugin boundary or generic simulation
framework is selected.

Success publishes immutable scenario and selection diagnostics. Movement entries
in composition come from the same fresh invocation, not duplicate evaluations.
Total RNG cost is |S| + actual emitted Attack costs + one/eight lottery draws;
destinations, baseline lookup, insertion and fallback cost zero. Every simulation
keeps its original starting ticks. No live state/RNG, damage, commands, facing or
native/editor flow is changed.

On any admission/scenario/Attack/lottery failure return **whole-query original
RNG**, no partial scenario, composition, rolls or plan. Preserve typed outer
stage and nested failure/index. In particular, a completed movement prefix must
be discarded if later Attack or lottery work exhausts counters. Check actual
visited draws; do not reserve eight when first-roll success needs only one.

## Acceptance for ZOD-145

- A non-adjacent in-reach case derives different D0/D1 and selects a real
  MoveThenAct candidate. An adjacent singleton uses an ActThenMove candidate.
  Compare against the legacy supplied selector with independently stated
  S/D0/D1 and literal source RNG costs, not only against another wrapper.
- Inactive target and zero-power/equal-baseline ranking both reach eight-slot
  MoveOnly fallback at D0. Preserve diagnostic absence versus a real score.
- Partial-hit first-roll success versus strict equality/all-failed use-first
  preserve exact modulo, visited slots and selected Attack, without rescoring.
- Missing new/selection facts and invalid weapon reject before movement even
  with TargetActive=false or exhausted RNG. Out-of-reach rejects without roll.
- Late movement, Attack and lottery exhaustion restore original query RNG and
  no partial result. Exact-MAX actual paths succeed; typed nested failures remain.
- Both actor identities, collection order, replay and explicit hypothetical RNG
  carry preserve original snapshots, HP/CT/choices and live RNG. Existing
  supplied-scenario tests remain valid; no test duplication of all prior guards.

Next source work should investigate a bounded out-of-reach branch, including
post-score reach-halving RNG and fresh versus unvisited D0 cells, before any
extension. Full target/ability search, Defend and command execution stay separate.
