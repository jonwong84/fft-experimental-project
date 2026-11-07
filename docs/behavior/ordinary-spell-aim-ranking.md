# Fixed-origin ordinary spell evaluation and ranking — ZOD-425

Start at [OrdinarySpellAiSearchQuery.Evaluate](../../src/Zodiac.Framework.Ai/OrdinarySpells/OrdinarySpellAiSearchQuery.cs).
It freshly binds a definition-owned catalog and learned primary slot, enumerates
potential aims at the current origin, simulates them in source order with one
decision RNG, and returns the strict best. This is a diagnostic for one spell
slot and one origin. It grants no command authority, does not adopt hypothetical
unit state, and accepts no caller-provided masks, scores or retained evaluations.

Managed baseline is `67f09d891e2739afb6c58debaa687cbf0a1fa4a0`. The read-only
reference remains `11464943bbdbd2a0af022d90cb7f10455cf911df`; the
[source receipt](../evidence/ai/spell-aim-ranking-source-v1.json) pins nine files
and thirty-three functions. It extends the preceding receipt with
`ai_action_rank_reset`, `rank_current_decision_clear`, `action_hits_simulate`,
`strikes_simulate` and `origin_destination_finalize`. No original or C code was
executed for this milestone. The existing BIOS-v3 RNG transition contract remains
the basis for managed seed/count checks; this ticket establishes bounded caller
draw ordering rather than re-verifying the generator.

## Follow the pipeline

1. `OrdinarySpellAiAimEnumeration` performs the existing fresh admission,
   working usage, usefulness, deadline grouping and layer/Y/X enumeration.
   The [ZOD-424 domain](ordinary-spell-aim-enumeration.md) remains in force.
2. `OrdinarySpellAiWork.ForAimPass` privately copies that admitted baseline and
   narrows valuable membership to the current source pass. Units outside that
   useful mask still receive real formula work, can contribute collateral and
   remain in absolute roster scoring. Public cached diagnostics are not inputs.
3. `OrdinarySpellAiSimulation` performs shared primary preparation, clock,
   stored-aim hit work, accounting and scoring. Each aim starts from the same
   immutable battle baseline. The preceding candidate's exit RNG becomes the
   next entry RNG, including a NotValuable result's real formula draws.
4. `AiCandidateRank` compares signed16 priority and then byte hit. Physical and
   ordinary spell callers share the same key comparison, while each formula
   family retains its own contribution records. Current origin/destination is
   constant, so foe proximity cannot distinguish an exact rank tie. The earlier
   source-ordered candidate wins; this is not expected-damage sorting.
5. The returned passes retain their potential lists, actual evaluations, entry/
   exit RNG and improvement markers. `Best` points to a fresh evaluated aim;
   absence of a best is a normal accepted result when nothing scores.

The initial rank matches `ai_action_rank_reset`: marker0, priority-32767, hit0.
The admitted <=16 bare-unit roster keeps ordinary absolute scores inside that
reset bound. This is best-candidate comparison, not movement-baseline admission,
ranked-list insertion, lottery or a complete decision maker.

## Early exits and caller corrections

`ai_action_evaluate` compares a scored result before
`origin_aim_tiles_evaluate` checks `ability_processing_done`. An unresolved charge
at the255-call clock limit can therefore become best with hit255. Only then does
that origin's current pass stop. The outer `ai_ability_outcome_evaluate` caller
can proceed to another unit/tile pass, retaining the decision RNG and best rank.
The managed result records `OriginStoppedUnresolved` for each affected pass.

`action_hits_simulate` sets `ability_processing_done` before strike processing,
even when the resulting hit is nonvaluable. Such results do not stop this
origin's aim scan. In the admitted fresh simulation domain only an unresolved
charge leaves that flag clear. General invalid preparation, other pending
actions, suspended frames, reactions and wider status branches remain gated.

This caller trace also exposed an override after grouping:
`origin_reachable_evaluate` clears the consume-evaluated-aims setting for an
exhaustive origin scan. The ZOD-424 potential pass diagnostic now reflects that
override, with its short-range evadeable scenario extended to check it. Aim
coordinates, grouping and existing supplied-aim simulation contracts are unchanged.
Dynamic consumption from caster outcomes matters to later origins; one-origin
passes visit each coordinate once, and each subsequent caller pass rebuilds its
own mask. Multi-origin consumption is still deferred.

Unsupported work rejects the entire composed query, discards earlier candidate
diagnostics/best and restores entry RNG. This includes a late draw-counter failure
and the existing immediate-caster-KO boundary. Ordinary NotValuable results retain
draws; they do not become unsupported-context failures.

## Evidence and next work

Eighteen new managed scenarios exercise unit-before-tile ties, a better negative
area rank, first equal-area retention, pass-specific valuable masks with real
collateral, nonvaluable RNG continuation, healing, unresolved pass-local exits,
full rollback after a previous valid candidate, fresh boundary rejection and an
empty search. The three-unit area scenario evaluates twelve aims, keeps
tile(3,1,0) at priority-401/hit100, consumes88 draws and ends seed3396639393 from57.
The congested allied scenario ranks hit255 twice across unit/tile passes,
consumes510 draws and retains the first exact tie without spending MP.

The [source description fixture](../fixtures/behavior/spell-aim-ranking-v1.json)
has four literal candidate-iteration traces. Four Python tests validate strict
keys, preserved draws, per-pass exits, input isolation and mutation detection.
These descriptions consume supplied candidate outcomes; they do not execute C#
or constitute a formula/original-game oracle. The
[implementation receipt](../evidence/ai/spell-aim-ranking-implementation-v1.json)
records full validation and owner preservation. No host pathway changed, so this
ticket does not introduce a new native presentation claim.

ZOD-426 will trace movement-origin selection and baseline ranks before selecting
the next runtime child. Multiple origins, full ability-list dispatch, list
insertion/lottery, actual magic AI commands, autonomous turns, broader status/
equipment/Reflect/trajectory/shape work, Golem, terminal/frame RNG, editor and
custom cone work remain separate. Owner drafts and immutable JobStats are intact.
