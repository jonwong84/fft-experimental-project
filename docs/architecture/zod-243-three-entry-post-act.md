# Three-entry preparation after actual ACT — ZOD-243

Source/design study, 2026-11-04, managed base `c5dab73`. Read-only fft_decomp
HEAD was freshly checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/three-entry-post-act-source-v1.json) pins the
selected functions. This extends the [pair post-ACT trace](zod-209-pair-post-act.md)
and [Accumulate source study](zod-229-accumulate-ai.md); it does not execute C,
the original program or a presentation host.

The selected children below are implemented in
[ZOD-244–246](../behavior/basic-skill-ai-post-act.md), retaining these boundaries.

## Actual state and entry

`turn.c::ai_command_build` checks saved-choice no-progress before refreshing.
After a real ACT changes the action flag, Move0/Act1 forces a new simulation,
refresh, ability considerations, reach and selection. An unchanged saved0/1
instead takes END before refreshing. The managed FreshRefreshEntry assertion
must continue to describe that earlier guard; Act-used alone cannot prove it.
Use actual post-command RNG, PA, HP/status, target displacement and facing.
The pre-ACT projected D1 is never a post-ACT Move instruction.

Actual self ACT adds no resolution draw and publishes capped PA+1; PA99 remains99.
`outcomes.c::ai_simulation_end` restores speculative state without rewinding RNG.
`clock.c::turn_end` refunds20 for unused Move during fresh post-ACT refresh.
Hypothetical staying also refunds20; hypothetical moving refunds neither choice.
Both cap at60 after u16 addition and before byte storage. Managed clock/scoring
kernels already implement this effect-free domain and remain the owners.

## Four initialized entries, three cleared actor actions

Both initialized lists are Attack, learned Accumulate, learned Throw Stone,
non-Frog Frog Attack and terminator. `abilities.c::ai_unit_abilities_init` counts
four entries, including unusable Frog Attack; two evadeable entries give a
quarter-share of2. Silence/paid-MP shares are0. These initialization summaries
are not recomputed from the final action-used usability bits.

The first pass of `turn.c::ability_considerations_set` counts two harmful and one
helpful entry, HasRangedAbility=true and CanHealAllies=false for each living unit.
Accumulate contributes helpfulness at PA99 too; its raw stat-hit scoring is not
consulted in this preparation pass. The actor's helpful ratio stays below the
mostly-support threshold; the enemy's retained mostly-support bit is unrepresented.
The second actor-only pass finds enemy minimum range1 and ally minimum range0,
then adds raw Move: enemy reach Move+1, ally reach Move. Ranges, first-pass counts
and ranged summary survive the later usability clear.

Accumulate's forced consume-aim policy bypasses usefulness classification in
this pass. Ordinary immediate Throw Stone classification is deterministic.
Finally, action_taken clears each actor entry's usable flag. The loader carries
that bit into `evaluation.c::ai_ability_outcome_evaluate`, whose sign-bit guard
returns before random-use/usefulness, targets, formula evaluation or candidate
clocks. **No Accumulate coin or action scan runs after this clear.** Frog Attack
was already unusable. Eight fresh ranked slots therefore remain empty.

## Movement and fallback

Fresh `scenarios_build` still evaluates movement from actual geometry. In-reach
filters by raw Move+target distance; out-of-reach filters by distance-1 with the
existing reachable fallback. Source scan/tie ordering and proximity remain in
the established movement kernels. Out-of-reach NonBlue consumes its modulo7
reach-adjustment draw after movement scoring; Blue skips it. Both post-ACT
branches copy D1 to D0, skipping the pre-ACT ranged distance-floor chooser.
The empty terminal lottery consumes eight rolls, then explicit NoDefendSupport
selects movement-only D0. Dispatch sends a different-tile Move or same-tile END.
No unknown retained cast-priority cell is read by an unusable action entry.

Existing `SwordAiMovementScenario` post-ACT internals need ordinary minimum
enemy range1, not an Attack-only ability list. The three-entry preparation proves
that range independently, so those lower kernels are reusable without forging
a pair/Attack-only list certificate. Existing pair scenario public outcomes and
all earlier APIs should stay unchanged. This can derive post-ACT movement even
while pre-ACT three-entry S/D0/D1 provenance remains caller-supplied.

## Admission and selected implementation children

Retain the closed living two-unit profile, matching grants/baselines/geometry,
zero equipment PA bonus, PA1..99 and reachable PA99 weapon envelope, raw Move<=124,
ordinary status/upkeep/cache assertions and both admitted teams. Blue requires
PLAYER_CONTROLLED clear and HP*128/maxHP>=45. Broader Blue policies stay separate.
Content/grant/setup validation must not call a spent ACT validator and then
ignore ActionAlreadyTaken; that early result skips later checks. Reuse pure
family admission with explicit choice mode and the existing targeted setup
validator. Do not fabricate an ACT-unused snapshot.

- **ZOD-244 — Fresh three-entry post-ACT preparation.** Add an explicit
  EvaluateAfterAct entry sharing pre-ACT admission and summary construction.
  Use existing post-ACT refresh, preserve actual PA and first-pass summaries,
  clear all three actor entries and return no partial output/RNG on rejection.
  Cover self boost/cap, actual damage/push, both grants, both teams, wrong choices,
  terminal input and exact draw/tick limits. Keep actual command validation intact.
- **ZOD-245 — Post-ACT movement scenarios and empty selection.** Own preparation,
  then existing lower in/out-of-reach movement kernels and the shared eight-slot
  lottery. Require fresh general-entry/ranking, normal/canonical/ordinary policy
  and NoDefend facts. Derive D0=D1 without action scans or usefulness draws.
  Preserve scenario diagnostics, actual state, source tie/RNG order and whole-query
  rollback. Do not accept cached preparation, movement or plan input.
- **ZOD-246 — Actual post-ACT movement and settlement.** Own that fresh selection
  inside Framework's existing callback; adopt verified RNG once and issue only
  turn-ending confirmed Move or Wait. Settle actual completion facing, preserving
  boosted PA and prior published ACT on failure. Add no continuation token,
  second ACT, repeated adoption or clock advance. Cover both routes/teams,
  authoritative guards and exact/late-budget recovery.

The killed sole opponent is explicitly rejected by this living profile. Source
nearest-enemy fallback and terminal encounter/controller policy need separate
work; reject without undoing ACT or inventing a living target/facing choice.
Complete three-entry turn orchestration, pre-ACT scenario derivation, broader
lists/maps/policies, editor/host activation and battle saves remain separate.
ZOD-106 stays deferred, boosted PA stays battle-only, and original frame/VSync
RNG interleaving remains unresolved.
