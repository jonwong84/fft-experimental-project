# ZOD-171 — Fresh preparation with both choices unused

Source/design, 2026-10-28; managed base
`b29a418683d1be6315964ef16fd54834adc4ad15` (ZOD-170).
Read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/unmoved-preparation-contract-v1.json) pins
fourteen function bodies and three additional files, extending the existing
[command](zod-165-ai-command-continuation.md),
[refresh](zod-156-ai-refresh-prefix.md), [preparation](zod-160-ai-preparation.md)
and [flat movement](../behavior/flat-ordinary-movement.md) studies. No production API,
original/C execution, playable AI activation or full equivalence claim is added.

## Entry and source ordering

The current fresh refresh/preparation APIs deliberately require Move used and
ACT unused. The [live Move-first adapters](../behavior/sword-ai-move-first-out-of-reach.md)
instead start at certified post-prefix general selection with both choices
unused. Calling the former from the latter would either reject or use the wrong
temporary CT and reachable tiles. No external certificate can turn an already-
moved refresh result into a fresh unmoved result.

`ai_command_build` performs its early no-progress/compact-record checks, installs
the actor through acting_unit_init and selects refresh only when undecided or
ACT already used. This study starts at a **fresh, non-resumed refresh entry**
with an undecided selection, active ordinary actor, both choices unused and no
pending presentation receipt. It does not implement entry guards or post-ACT
reselection. The early cached actor delay is still captured before refresh;
CleanActorDelayCache remains explicit.

The order is refresh, ability_considerations_set(action_taken),
reachable_tiles_build(movement_taken), team-policy update, target consideration,
then action_select. Flags/list/cache facts absent from managed state remain
explicit inputs under the same stable, living two-opponent None/Critical,
effect-free ordinary sword profile. Fresh full-prefix preparation remains NonBlue;
the post-prefix Blue adapter does not prove Blue preparation policy equivalent.

## Refresh refunds both unused choices

`decision_flags_refresh` sets current action act_before_move=1 and skillset=0.
In `ai_action_simulate_and_score`, the action-first movement/pickup call is inside
the nonzero/non-Defend action block. That whole block is skipped. Refresh neither
moves nor consumes ACT, and stale candidate coordinates/pickup data are not used.
Its flag rebuild and retained ConserveCt/TeamSupportUnavailable bits follow the
existing targetable, zero-cached-MP contract unchanged.

`turn_end` reads CT into a **u16**, adds TURN_UNUSED_CT_REFUND=20 independently
when each choice is unused/unrestricted, then caps at TURN_END_CT_MAX=60 before
storing the unit's u8 CT. With both choices unused and no Don't Move/Don't Act/
Performing restriction, refresh end CT is **min(60, liveCT+40)**. Already-moved
refresh remains min(60, liveCT+20). Do not mark Move used to fit the old API or
apply byte wrapping before the cap. Active ordinary CT is 0..99 in this profile;
Quick, restrictions, mounts and charge behavior remain excluded.

The simulation ends the actor's temporary turn and runs from UNIT_TURN with
resume PICK_UNIT through effect-free end phases. An already-ready peer is checked
before AddCt. CT gain uses the existing 254 cap; highest CT wins, then original
slot/TieKey order. The selected living unit gets CT modulo100, one upkeep draw
and temporary unused choices. The simulator records its movement opportunity,
clears temporary has_turn, sees no charges and stops. This closed pass has exactly
one gameplay draw. Frame suspension/VSync BIOS draws remain outside the managed
description, as in earlier contracts.

The baseline save/restore restores unit identity/status, CT, choices, HP/MP,
position/facing, action records and team Golem; between-turn globals are restored
separately. Refresh-owned workspace flags survive, but temporary clock state is
diagnostic only. RNG is retained. Preparation after this uses the **original live
snapshot**, not the units returned by the hypothetical clock. No live ticks,
revision, facing receipt or session are changed by a pure query.

## Reachability and restored-state preparation

Unlike the movement-taken shortcut, reachable_tiles_build now calls move_range_build.
In the existing flat, unit-cost, no-special-trait profile, the source spread's
Move+1 residual origin and frontier passes produce the already-traced physical
reachable tiles. Scenario0 copies destination marks, removes other occupied unit
tiles and restores the actor's own tile. Known traps, teleport, riding, stepping
stones, weather/terrain effects and other movement policy remain excluded by the
existing certificate; admitting a full ordinary spread does not admit them.

Reuse FlatOrdinaryMovement on the restored unmoved snapshot. Require consistent
body HasMoved and turn MovementTaken values. Its AI origins contain the current
tile plus all legal reachable destinations, in layer/y/x order. Move0 legitimately
gives a singleton even though Move is unused; it does not certify an actual Move.
No scoring, upkeep, movement command, path settlement or RNG occurs here. Keep
raw Move<=124 for this ordinary preparation/scenario envelope; this is a bounded
compatibility adapter limit, not a permanent content/application limit.

The closed usable list remains ordinary range-one Attack, unusable Frog Attack
and terminator. ACT is still unused, so existing no-cost/no-charge ability
preparation, NonBlue support policy and sole-enemy focus derivation apply. Actor
reach is raw Move+1, ally reach Move. No extra draw follows refresh. Delays and
proximity use restored live CT: ceil(max(0,100-CT)/Speed), with proximity admitted
when target delay<=actor delay. They must not use the first hypothetical ready
unit or its modulo CT. Normal scoring, canonical coordinates, ordinary scenario/
tile policy, target-selection equivalence and no-Defend remain separate assertions.

## Next bounded implementation: ZOD-172

Add explicit pure unmoved entry points to refresh and preparation while preserving
the existing already-moved APIs and their admission. Share owned calculations;
require active actor, Move/ACT unused, valid policy rows and all current profile
certificates. The new refresh uses the two refunds; preparation owns one fresh
refresh, derives ordinary reachable origins and closed policy/focus on the
original immutable snapshot. Expose immutable diagnostics and progressed RNG
only. Do not accept cached refresh/preparation/geometry outcomes or synthesize a
trusted session. Geometry failure must hide partial refresh output/RNG too.

Verify the vectors below against managed execution, exact draw/tick limits,
original snapshot isolation, both actor IDs/order replay and old-entry rejection
on unmoved input. Reuse current flag/geometry/clock regressions instead of copying
their complete matrices. No new interface abstraction or broad default-policy
rewrite is needed. Later owned preparation-to-selection composition and live
Move-first integration require their own bounded validation; keep current
GeneralSelectionEntryContext until those own the full prefix.

## Checkable vectors and validation

The [fixture](../fixtures/behavior/ai-unmoved-preparation-contract-v1.json)
contains seven clock and two reach descriptions. CT15/95, Speed5/5 temporarily
ends actor CT55, adds once and picks peer, leaving temporary actor60/peer0.
Seed word1/count7 consumes raw16838 and ends word1103527590/count8.

CT15/40, Speed10/10 ends actor55 and picks the actor after five additions, leaving
actor5/peer90. Reusing the already-moved refund would instead pick the peer after
six additions. Restored preparation delays remain actor9/peer6; the hypothetical
actor CT5 must not replace live CT15. A reversed-order CT95/60 Speed20 tie confirms
the cap and slot-order pick. Already-ready-peer at MAX ticks, exact one-draw MAX
and exhausted draw/tick proposals retain the existing managed rollback policy.

On a 5x3 ordinary board, actor(3,1)/enemy(1,1), Move1 yields the five ordered origins
(3,0), (2,1), (3,1), (4,1), (3,2), adding no draw; Move0 yields just (3,1).
These are authored scalar reductions, not original or managed prefix executions.
Three Python checks cover the descriptions, input preservation, restored-state
distinction and malformed entry/counter inputs. ZOD-172's managed checks remain
unexecuted until implementation. Source fingerprint drift fails the separate audit;
current managed files are not frozen by historical hashes.

Reproduce with:

```powershell
python tools/verification/verify_ai_unmoved_preparation_contract.py --reference C:/repositories/fft_decomp --output <new scratch report>
python -m unittest discover -s tools/verification -p 'test_ai_unmoved_preparation_contract.py'
```

Full validation and logs are recorded in the current handoff.
ACT-first/post-ACT policies, full search, saveable continuations, frame fidelity
and host/editor activation remain outside this source/design ticket.
