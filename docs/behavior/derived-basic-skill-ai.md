# Derived three-entry AI scenarios, selection and turns — ZOD-250–254

Implemented 2026-11-04 from managed base `d9375d2`, following the
[ZOD-249 source study](../architecture/zod-249-three-entry-scenarios.md).
Read-only fft_decomp was checked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/derived-three-entry-source-v1.json) pins 35
functions across eight files; the
[implementation receipt](../evidence/ai/derived-three-entry-implementation-v1.json)
records the managed checks. No original or C program was executed.

## Shared fresh scenarios — ZOD-250

`BasicSkillAiScenarios.Evaluate` owns one three-entry preparation followed by
automatic in/out-of-reach routing. The private `AiMovementScenarios` kernel
also serves the existing pair wrappers. Their public result shapes, explicit
branch guards, mask identity and RNG streams remain unchanged. Preparation
belongs to each family; no pair-list certificate is fabricated for Basic Skill.

The kernel fills proximity for original reachable M0, filters the source score
mask S, evaluates movement once and then makes the NonBlue out-of-reach reach
draw. Blue skips that draw. It derives D0/D1 using the existing key/distance/
later-coordinate ordering, ranged pre-ACT bound and distance fallback.
Scenario2 is M0 minus S before actor insertion. Final scenario1 adds the actor
without scoring it and can overlap scenario2. `AiDerivedMovementScenario`
reports masks, destinations, branch/fallback diagnostics and every original
cell's fresh proximity and nullable written priority. Null remains unknown.

## Provenance-safe composition — ZOD-251

`BasicSkillAiDerivedComposition.Evaluate` takes state, the existing preparation
request, `BasicSkillAiDerivedProfile` and explicit RNG. The profile retains
ordinary scenario, general-entry, fresh-rank and clear-direct-trajectory facts.
It accepts no supplied S/D0/D1, cached prefix, prior ranks or cast-key assertion.
Prepared entries derive scan policy and the movement baseline facts.

Any unwritten final scenario1 tile within the hostile target's Stone range4
rejects at Provenance before action scanning. This covers the shorter Attack
range too. Outside that diamond the tile cannot be read by either damage scan;
the private damage scan omits it. Accumulate still evaluates its self-map target
and does not enumerate cast keys. The guard applies even if self might win.
There is no zero default, actor rescore or assumed history.

Thus ordinary in-reach cases and the proved unread out-of-reach cases work.
At Move1 with target distances3/4 the inserted actor's priority is unknown and
selection rejects atomically. At distance5 it is safely unread. At Move0 the
empty closer filter falls back to M0, writes the actor and admits range4 Stone.

Private fresh scores/RNG enter the existing composition owner, preserving
Attack → self usefulness coin/hypothetical clocks → Stone, independent candidates
from actual input state and shared rank insertion. Preparation/movement are
never repeated between entries. A failed pure query returns its incoming RNG
and no partial outcome.

## Terminal selection — ZOD-252

`BasicSkillAiDerivedSelection.Evaluate` runs that composition then the same
terminal lottery as the supplied-scenario API. Existing terminal Stone,
NoDefend and ordinary default assertions remain explicit. Empty ranks take
eight draws and return MoveOnly at derived D0. Success returns scenario and
scan provenance alongside the existing typed selection. Queries grant no live
command or RNG authority; late lottery rejection hides all progressed work.

## Actual first command — ZOD-253

`BasicSkillAiCommandExecution.ExecuteDerived` owns derived selection inside
the existing Framework callback, then shares its guards and typed dispatch
with supplied-scenario execution. It returns the existing command result and
actual Move, Attack, Stone, self ACT or END. Scenarios/Provenance are appended
selection failure stages; old enum values and result shapes remain intact.
No public selector callback, cached result, session or receipt authority is added.

Actual rules adopt only the verified decision RNG and resolve real effects.
Self ACT gains PA1 up to99 with no resolution draw. Projected pre-ACT PA/CT
and predicted D1 are diagnostics. Move-first damage uses the existing frozen
Attack/Stone token, and empty Move uses the existing Wait token.

## Complete turns and recovery — ZOD-254

`BasicSkillAiTurnExecution.ExecuteDerived` takes
`BasicSkillAiDerivedTurnConfiguration(BeforeAct, Selection, PostActScenarios)`
and the existing dispatch assertions. It shares one preparation configuration
with automatic post-ACT selection and enters the existing turn stage machine.
The supplied entry remains supported. The private frozen inputs retain the
chosen first-entry mode; the opaque recovery cursor cannot switch it.

FirstCommand, RetainedCommand, PostActCommand and FinishFacing preserve actual
damage/PA/Move/RNG as each command publishes. Resume requires the exact returned
partial session and retries only the failed stage, returning only new events
and decisions. Failed private work publishes no RNG or speculative diagnostics.
The driver adds no next clock, self-Move token, terminal policy or persisted
checkpoint. Lethal ACT-first and Move-first stop at the same explicit terminal
post-ACT/facing boundaries, retaining accepted progress and pending receipts.

## Literal streams and validation

Vectors start at origin(1,1), word1/count7, PA5, CT15, Speed5/5, with zero evade
unless stated. Target HP starts50. Tests bind input immutability and ordered events.

- Move0, target(2,1): Attack query ends word2937909499/count21; actual Attack
  ends word3823029677/count27 and deals10. The complete turn ends
  word3834121963/count37, CT35, HP40 after two fresh decisions.
- Move0, evade97, incoming word7: self wins after both 3% damage rolls fail.
  The turn ends word2073554906/count34, PA6, CT35, HP50, two decisions.
- The PA99 variant uses word1 and ends word1303003706/count36, PA99, CT35.
  Both hypothetical damage projections KO, so their clocks visit the corpse
  before the actor and shift the self coin to draw9. This is a different
  source schedule; it must not reuse the PA5 witness's draw count.
- Move1, target(6,1): query ends word267834847/count17; actual Move/Stone and
  receipt facing end word3791773014/count24, CT15, HP40, one decision.
  Blue skips the reach draw and its query ends word3210001534/count16.
- Move0, target(5,1): fallback scores the actor; ACT-first Stone plus post-ACT
  settlement ends word3008955749/count35, CT35, HP40, two decisions.
- Move0, target(6,1): empty END ends word836760821/count19, CT55, HP50,
  one decision. No action is invented after eight empty lottery draws.

Twenty-one new AI cases cover these streams, derived/supplied parity, written
and unread provenance, masks/actor overlap, no-rescore ordering, exact/late
budgets, actual state and events, every recovery stage, lethal boundaries,
both teams/identities/collection orders and whole-query rollback.
Locked restore and full Release build pass with zero warnings/errors;
**4,242 .NET cases** (1,467 Framework, 1,030 AI), **539 Python tests**, source/
prepared-data, algorithm-guide and coverage checks pass. Logs and seven TRX
reports are in ignored `scratch/derived-basic-skill-249-254/`. Use the
[existing reproduction and Python/Pillow setup](accumulate-ai.md#validation).
Historical checkpoint snapshots were not rerun. No host/editor or UI review.

ZOD-255 next traces retained cast-priority lifecycle and its concrete bounded
consumer. Unknown keys remain rejected pending source-backed representation.
General lists/maps/rosters/policies, terminal encounter routing, replaceable
interfaces, frame/VSync RNG and host activation remain separate. ZOD-106 stays
deferred; boosted PA is battle-only. All owner definition drafts, including
AbilityToggles, remain unchanged.
