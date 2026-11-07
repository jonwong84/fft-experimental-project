# ZOD-118 — End-of-turn facing and completion contract

Date: 2026-10-26. Managed base: 4327b78, clean on entry. The read-only reference
C:/repositories/fft_decomp was freshly checked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df. All source symbols here refer to that
revision; [evidence](../evidence/ai/turn-facing-contract-v1.json) fingerprints
12 files / 32 functions. This extends the [ZOD-116 map](zod-116-ai-decision-contract.md)
and follows the completed [sword formula query](../behavior/sword-ai-formula-projection.md).

The original can finish a unit's CT turn **before** choosing its final facing.
The managed engine needs a separate, attributable completion boundary. Adding a
facing parameter to the current active-unit Wait API would miss automatically
completed Move+Act turns. This spike specifies an opt-in immutable wrapper and
its next implementation milestone; it introduces no production C# or AI policy.

## Original call and state ordering

Source paths below are under src/battle unless qualified.

1. turn/controller.c::action_execute_ability calls turn_act_taken_set before
   committing the action preparation. move_confirm marks Move after the accepted
   destination has been registered. turn/clock.c::turn_move_act_taken_set records
   the requested choice and calls turn_end when turn_should_end succeeds.
   For ordinary unrestricted units this happens when both choices are consumed,
   in either order. Combat, displacement and post-command work can still follow
   that clock completion. A TurnEndedEvent inside a managed Attack event list
   is therefore not permission to capture a partly resolved combat snapshot.
2. clock.c::turn_end exits immediately for an absent unit or one with has_turn=0.
   Otherwise it adds 20 CT for each unused, unrestricted choice, caps at 60 and
   clears has_turn. It does not set movement_taken/action_taken and draws no RNG.
   No-choice Wait from CT 15 leaves 55; Move-only or Act-only leaves 35; both leaves 15.
   Restrictions/Performing alter the general refund and remain outside the
   ordinary managed profile. Repeating the source call after completion adds
   nothing; the existing managed Wait instead rejects an inactive actor.
3. controller.c::battle_state_ai_command_update handles AI EndTurn by setting
   runtime turn_action_flags MOVED|ACTED and calling turn_end. Those display flags
   are distinct from the stats choice flags, so unused CT refunds still apply.
   Ordinary actions mark their actual choice. Do not simulate AI EndTurn by
   confirming both unused choices through the managed clock.
4. AfterCommand waits for its window/numeric display, then ContinueTurn waits
   for camera motion to finish. battle_state_continue_turn_update routes an ended
   ordinary unit turn through the Wait menu, even if Move+Act already ended it.
   Charged-ability events, Jump, scenario interception and statuses that block
   Wait have other routes; those are exclusions, not arbitrary extra facing calls.
5. battle_state_wait_menu_update enters WaitDirection on YES. NO/UNRESOLVED_FF
   also enters it when turn_is_over returns 1; otherwise it returns to commands.
   battle_state_wait_direction_enter reads the runtime's current facing and calls
   ai_wait_facing_choose for nonplayer-controlled units at this late stage.
6. battle_state_wait_direction_update applies cardinal hints and, once the
   **old** frame counter is at least 0x1f, calls turn_end again, publishes the tile
   and facing through tile_position_with_partner_set, then starts the next event.
   Player confirmation uses the same publication path. The repeated clock call
   is harmless in C because has_turn is already clear. No facing helper here
   draws RNG; surrounding battle_scene/menu/event updates are not a proven
   zero-draw interval for the whole original controller.

The owner's brief enemy Move/Act pauses belong to host presentation. The future
deterministic transition will not inspect frame counters, sleep or advance the
battle clock to reproduce a presentation delay.

## Direction choice, data and observable quirks

ai/evaluation.c::ai_wait_facing_choose returns a stored hint directly unless it
is ChooseAtWait. include/fft/ai_decision.h defines South 0, West 1, North 2, East 3,
Overlap/KeepCurrent 4 and ChooseAtWait 5. The controller only rotates for hints<4;
an explicitly stored 4 safely keeps the runtime facing. This differs from the
unsafe lookup that can produce 4 while choosing. A typed adapter must validate
the admitted hints rather than accept arbitrary original byte values.

Runtime cardinal angles are South 0x000, West 0x400, North 0x800, East 0xc00, divided
by UNIT_FACING_QUARTER_TURN=0x400 on publication. Managed BattleFacing is East 0,
North 1, West 2, South 3. Map **by name**, never by casting original numeric values.
South is decreasing map y; North is increasing y. Screen orientation is separate.

For ChooseAtWait, ai_unit_coordinates_copy refreshes the actor's current x/y/layer.
facing_is_tile_blocked tests four offsets in South/West/North/East order. Adjacent
x/y are bytes, so negative offsets wrap to 255 before bounds rejection. The target
tile is always on the **lower** layer; source height uses the actor's actual
layer. Out of bounds, blocked or height rise>=3 rejects that direction; rise 2
and arbitrary descents pass. Half height, slope, depth, occupancy, Untargetable
and CannotStop are not consulted by this helper. This is a facing viability
test, not a movement or landing legality query. All four directions become
viable if none passes. The last direction that passed is the fallback otherwise.

The chooser resets live unit_targetable flags, then uses ai_target_find_nearest:

- Try nearest imminent enemies, removing a tried target from that imminent scan
  if its direction is not viable, until a viable direction is found or none remain.
- Then try only the nearest eligible enemy of any kind. That search ignores the
  live exclusions made above and can select the same failed imminent target again.
- If this last direction is not viable, keep the current stats facing if viable;
  otherwise use the last viable direction in source order. It does not try the
  second-nearest non-imminent enemy just because that enemy points somewhere viable.

Manhattan ties keep the first original unit slot, independent of managed ID or
collection order. All relevant nearest modes exclude untargetable, petrified and
dead units. ai_target_is_untargetable also checks presence, crystal/treasure,
mounts and hidden enemy rules, including the documented four-bit partner-ID
quirk. Enemy flags originate from decision_flags_refresh, which compares other
units' initial team to the actor's current team; they cannot be inferred from a
job or collapsed into a general current-team test.

An imminent enemy must act no later than the actor's cached remaining-clockticks
estimate and not remain disabled by Stop, Don't Act, Sleep, Chicken or Charm
through that delay. Near-adjacent enemies more than two whole heights below
the actor are also excluded. ai_height_difference_calculate uses signed
half-height difference divided by 2, truncating toward zero. ai_unit_turn_delay_calculate
and ai_status_is_active_through_delay supply the cached timing/status inputs.
The description chooser takes **normalized eligible-enemy/imminent flags**;
it does not implement these workspace/team/status calculations or refresh them
from the new CT after Wait.

turn.c::ai_target_facing_find reads x/y as signed bytes. For the first small-map
profile coordinates stay 0..127; the direction helper also records the source's
wrap at 127/128. In terms of dx and dy, it splits along dy-dx and dy+dx. Exact
diagonals choose SW=South, NW=West, NE=North and SE=East. Overlap returns 4.
ai_wait_facing_choose then indexes a four-byte viability array with that 4,
reading the never-written past_viable_directions stack byte. If no enemy exists,
the nearest search returns the actor itself and reaches the same lookup. QUIRKS.md
records the overlap issue. The managed chooser will return Unsupported for both
cases; this is an explicit admission limit, not a discovered original outcome.
An explicit fixed/KeepCurrent hint does not perform that unsafe search.

## Managed gap and movement limit

TurnCycle.Apply already handles both choice orders and the ordinary CT refund.
AttackBattleSimulator.Move/Attack can return PickUnit/no active actor after the
second choice. Current result objects have no receipt, revision, last-ended actor
or pending-facing field. Ticks and RNG count do not uniquely identify a turn:
multiple peers may act without another tick, and Wait/Move do not draw RNG.
Bare actor ID, CT, choice flags, or a saved PickUnit snapshot cannot authorize a
later facing change. Current TwoUnitBattleScenario also rejects every command
after its authored first-KO outcome; this contract does not change that lifecycle.

Original units/movement.c::move_step_start_walk turns an ordinary walking unit
to the path step heading, except in ACTION_EXECUTE (such as knockback).
move_settle_on_tile and controller.c::move_confirm publish the settled tile and
runtime facing. Managed Move only updates position/choice; it preserves incoming
combat facing and does not retain a path. KeepCurrent in the next milestone
therefore means the **supplied managed settled facing**. It cannot establish
original post-path heading parity. Do not derive that heading from net displacement
or alter existing movement fixtures silently. Original path/facing composition
remains a separate prerequisite before a full compatible selector/encounter claim.
Sword knockback's preserved settled facing remains consistent with the traced
ACTION_EXECUTE exception; final choice must see the displaced target position.

## Proposed bounded application boundary

These are design names, **not production types or an implemented interface**.
Recommend an opt-in Zodiac-owned immutable FacingTurnState wrapper over the
existing AttackBattleSnapshot and FlatSwordBattleProfile. Its constructor is not
public. Begin accepts a validated healthy fresh AddCt setup, with no active unit
or consumed choices, and revision 0. It must reject attaching to an arbitrary
active/ended/restored snapshot. Exactly two ordinary units and certified open,
dry, flat lower ground remain required, with width/depth at most 128 so all unit
coordinates stay below signed-byte wrap. This has no scenario selection, restore
or save representation.

The minimum additional state is an accepted-transition revision and optional
pending-facing receipt. A receipt is internally minted, retains the completing
actor and revision, and binds to the exact **fully settled** immutable battle
snapshot and its wrapper lineage. The request must carry that very receipt.
Matching only actor/revision values is insufficient across branches or independent
runs. An implementation may use opaque internal receipt identity plus the expected
snapshot reference, as the existing restore boundary already binds immutable
definition instances. These in-memory identities are authorization capabilities,
not random game inputs, serialized tokens or a cryptographic trust boundary.
Replay creates fresh receipts and compares semantic state/events/revisions.

The wrapper owns these operations; it offers no public WithBattle/AttachReceipt
that bypasses them:

1. **Advance, Move and Attack** delegate to the existing rules after admission and
   revision-capacity checks. Every accepted state mutation increments revision;
   rejection publishes the identical wrapper and no events. Advance keeps its
   existing readiness draw; Attack keeps its existing execution draws. If a
   successful Move/Attack completes the active turn, collect the matching
   TurnEndedEvent and mint the pending receipt only after the entire result,
   including HP/status/KO/displacement/RNG, has settled. Keep those already
   committed effects even if a later facing query cannot choose a direction.
2. **WaitWithFacing** applies to the current active actor with no pending receipt.
   Validate/resolve a typed choice first, then atomically publish legacy Wait
   and the facing update. There is one CT completion/refund and no new draw.
   In the closed two-unit flat profile, fixed/KeepCurrent or choosing the one
   certified living enemy's direction is independent of the actor's refund.
   This collapses the source's separated controller stages only within that
   profile; it is not a general replacement for cached multi-unit imminence.
3. **FinishFacing** requires the matching pending receipt, its actor, its exact
   expected settled snapshot and revision, with no active clock unit. Apply only
   facing, clear the receipt and increment revision. Do not call Wait, ConfirmAct,
   Advance, repeat the command or replay damage/RNG. CT, flags, ticks, position,
   both RNG copies, PA/HP/KO and every other unit remain unchanged. Emit one typed
   facing-settled event, even when the selected facing equals the existing value;
   same-facing completion still consumes its authorization. Do not emit a second
   TurnEndedEvent or movement event.
4. While a receipt is pending, reject Advance/Move/Attack/another Wait with
   FacingPending. A failed or unsupported facing request preserves that pending
   state for a valid retry. Missing, stale/cross-branch or wrong-actor receipts
   reject atomically. Revision exhaustion rejects before delegating or publishing
   any effects. It must not be mistaken for RNG exhaustion: choosing/applying
   facing and ordinary Wait require zero draws even at ulong.MaxValue RNG count.
5. Pure facing probes do not mutate state, consume the receipt or increment
   revision. On the authoritative immutable state chain, a second FinishFacing
   against the **returned** settled state rejects because the receipt is gone.
   Replaying the original pending input produces the same result; no global
   token-consumption set or mutable hidden history is permitted. The owner must
   retain the latest returned wrapper. A naked legacy call on an extracted
   snapshot cannot be reattached to this wrapper.

The first typed choice set is Fixed(BattleFacing), KeepCurrent and
ChooseClosedEnemy. Fixed and KeepCurrent do not require an adjacent move to be
legal. ChooseClosedEnemy requires explicit certification that the other ordinary
unit is an enemy, current living/targetable state and distinct coordinates; do
not infer teams from IDs or add production roster types. With one eligible enemy
on the certified flat rectangle, imminent-versus-any selection yields the same
direction. General multi-enemy/terrain/status helpers below remain source
descriptions, outside the first production profile. A dead/no-enemy chooser is
Unsupported; fixed/KeepCurrent remain separate operations over a living actor.
The first-KO host's terminal policy is not changed or bypassed.

This boundary is small in-memory state needed to finish an already modeled
turn. It is not the future IBattleDecisionPolicy continuation, an authoritative
decision-RNG commit runner or a midbattle save feature. Those may compose with
it later through separately traced contracts.

## Vectors, evidence and next milestone

The [description fixture](../fixtures/behavior/turn-facing-contract-v1.json)
has 70 vectors across five families: 14 direction, 10 viability, 19 normalized
chooser, 8 clock-end and 19 proposed completion-gate descriptions. Source-derived
outcomes are labeled separately from the three managed admission limits and the
19 managed design proposals. The gate rows describe validation/dispatch only;
Delegate does not claim that a subsequent Advance/Attack draws zero RNG.
The descriptions do not run C# facing or constitute an original executable oracle.
FTF-001..018 are all **unexecuted production cases**, with an explicit first-profile
implementation inventory. General helper vectors do not silently enlarge it.

All **359 Python tests pass**, including 17 new tests covering literal directions/enum names, signed-byte wrap,
height/flag/edge viability, retry ordering, slot ties, unsafe cases, once-only CT,
pending barriers, stale/wrong/missing authorization descriptions, exhaustion,
classification/tampering and input immutability. Full Python suite result and
matching final audit report hashes are recorded in the evidence. No C# changes
or .NET rerun; the last full implementation validation remains ZOD-117's
3,271 .NET / 342 Python tests. Existing source/fixture/implementation evidence
and thirteen managed baseline files remain unchanged. No original/native launch.

Recommend **ZOD-119: implement the opt-in bounded facing wrapper and closed-enemy
chooser** above. Execute FTF-001..018 plus the admitted direction/hint vectors,
both choice orders, actual post-push snapshots, same-facing consumption, all
receipt failures, every revision/RNG boundary and immutable prefix/suffix replay.
Keep production types internal in construction, preserve all legacy APIs/fixtures/
save/headless regressions and run locked build/full suites. Do not activate a
scenario/native path or save format. General normalized roster/status/imminence,
path heading, full original AI search/scoring/candidate clock and interface/session
RNG remain distinct follow-ups. Editor work and ZOD-106 remain deferred.

## Reproduction

~~~powershell
python -m unittest discover -s tools/verification -p "test_*.py"
python tools/verification/verify_turn_facing_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/turn-facing-118-source-final-a.json
~~~

Repeat the audit into a fresh final-b path and compare bytes. Ordinary tests need
neither C checkout nor original assets; the optional source audit reads only.
The managed-baseline flag checks this checkpoint and should be omitted after
intentional future C# changes. Use explicit UTF-8/LF fingerprints. Reports and the
description/evidence builder stay ignored under scratch/turn-facing-118-* and
scratch/build_turn_facing_118.py. No raw game bytes, C runtime dependency,
native capture, deployment or editor changes are introduced.
