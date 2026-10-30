# ZOD-112 — Sword settled-facing source contract

Date: 2026-10-26. Edition: SCUS-94221. This is a completed source/fixture
investigation, **not an implemented correction or original-game observation**.
Fresh read-only reference HEAD was clean at
11464943bbdbd2a0af022d90cb7f10455cf911df. Managed baseline:
66ce8c7 (full identity in the evidence). No production code, old fixture, save,
content package, native capture or historical evidence is changed by this ticket.

The bounded sword currently publishes its temporary attack-facing turn. At the
supported settled command boundary it should retain the living actor's incoming
facing. Target facing already agrees within this restricted profile. This can
change later evasion and RNG consumption, even when the first hit's damage and
event list do not change.

## Source and ordering

All source paths below are relative to the pinned reference checkout. The
[evidence](../evidence/combat/sword-facing-contract-v1.json) fingerprints files and
named functions with line anchors; line numbers alone are not identity.

1. combat/preparation.c::action_resolve_strike builds targets and calls
   action_calculate_target_result before presentation turning. In the ordinary
   weapon formula, combat/formulas.c::formula_weapon_damage calls physical evade
   and weapon damage. combat/formula_core.c::formula_evade_calculate_facing reads
   the **target's current stats facing** to retain class evade only from the front.
2. turn/controller.c::action_start_next_strike first resolves the strike, then
   calls action_apply_strike_results directly for basic attack (ability 0).
   action_apply_strike_results turns the casting unit toward the target when
   continue_attack_count is zero, before applying target results/animations.
   Turning therefore does not retroactively affect this strike's computed evade.
3. unit_facing_turn_to_target rejects null, a status in
   UNIT_STATUS_PREVENT_FACING_MASK, Reflect, or zero displacement. Otherwise
   facing_before_attack_save saves the old runtime facing **only if attack_facing
   is -1**. The new runtime angle is quadrant-snapped and written to battle stats
   by units/placement.c::unit_set_tile_position. Continued turns must not overwrite
   the first saved facing. Continued strikes, Reflect and mounts remain excluded
   from the managed sword profile.
4. units/animation_policy.c::action_start_target_response can temporarily turn a
   target for guard/dodge/catch responses. In our profile, class-evade misses may
   use the dodge branch; a normal damage/death response does not call that turning
   helper. These visual changes are not settled simulation commands.
5. combat/resolution.c::action_apply_target_result applies hit damage and Dead at
   zero HP. units/animation_policy.c::unit_target_displays_update and
   combat/presentation.c::display_update refresh runtime status flags before
   restoration. A miss returns without HP changes. Actor death from reactions,
   absorption or other additional effects is outside our closed sword profile.
6. turn/controller.c::battle_state_resume_attack_phase_update handles continued
   strikes, then calls combat/outcomes.c::action_post_action_facing_stub, which is
   **empty**. combat/preparation.c::action_store_actor_data records mimic-facing
   and positions, or restores a pre-reaction command; it does not clear the saved
   runtime facing or replace the later restoration. Target displays are refreshed.
   With no further action phase due, reward_grant is called.
7. reward_grant calls facing_all_after_attack_restore **before** reward processing
   and post-action reports, independent of whether a useful reward is awarded.
   The helper visits present runtime slots and calls facing_after_attack_restore:
   a living unit with a saved facing restores runtime and stats facing and clears
   the saved value; a dead unit **only clears** it. A living unit with sentinel -1
   keeps its current facing. No RNG call appears in these save/turn/restore helpers.
8. Post-action reports call action_end_command, then AfterCommand enters
   ContinueTurn. ContinueTurn reopens action menus or the Wait menu. The later
   wait-direction UI can explicitly choose another facing; it is separate from
   attack restoration. The managed Wait API currently has no direction selection.
   This ticket does not add one or claim complete original turn presentation.

## Representation and scope

include/fft/unit_runtime.h defines runtime facing at offset 0x070 and attack_facing
at 0x072 as signed 16-bit values. The -1 sentinel must remain signed; valid cardinal
angles are South=0, West=0x400, North=0x800, East=0xc00. A quarter turn is 0x400.
include/fft/unit.h::unit_position_t packs stats facing into four unsigned bits;
the ordinary values are 0/1/2/3 respectively. unit_set_tile_position receives an
unsigned byte and stores the nibble. Unit initialization sets attack_facing=-1.

Managed BattleFacing uses East=0, North=1, West=2, South=3. Map these by name;
never cast an original angle or quarter-turn value directly to this enum.
Cardinal managed geometry is +X East, -X West, +Y North, -Y South. Current sword
eligibility already excludes diagonal/ranged targets and requires the tile beyond
the target to be off-map; preserve that restriction.

The supported actor is alive at entry and cannot become dead in the closed
single neutral sword hit. Preserve actor.Facing in the returned snapshot on hit,
miss, zero damage, critical and target KO. Preserve target.Facing too: class-evade
visual turns are restored while alive, and the supported lethal damage path does
not introduce a target turn. **Do not generalize this to restoring dead actors or
previously turned dead targets**; the source explicitly skips that restoration.
Reactions, mounts, multi-strike, resurrection, direction choice, animation and
reward state are separate future capabilities.

There is no need to expose a transient saved-facing field in AttackBattleSnapshot
or serialize attack_facing. The pure command collapses the complete supported
resolution into a settled state. Act/Move/CT ordering, target HP/status/KO count,
attack event ordering and this command's RNG must remain unchanged. Do not emit
an invented settled-facing-change event for a temporary turn that is not published.

## Independently checkable vectors

The [fixture contract](../fixtures/behavior/sword-settled-facing-contract-v1.json)
is a separate description format. It is **not accepted by the existing executable
behavior-fixture runner**. Offline calculations were repeated; all ZOD-113
implementation acceptance cases remain unexecuted.

Its 32 cardinal vectors combine four attack directions, all four incoming actor
facings, and normal hit/forced class-evade miss. Actor starts at (1,1) in a dry
3x3 map and targets an adjacent boundary tile. Both units have HP50, PA5 and
power5, no modifiers, equal zodiac, and explicit zero equipment evade. Seed1 with
target class evade10 from the front hits for25 after six draws, ending at
2516284547/6; class evade100 misses after four draws, ending at 3295386429/4.
Target faces the actor. All results retain actor incoming facing; the old policy
instead yields the geometric attack direction. The fixture also records a target
KO at HP25, critical seed57 (damage45, seven draws, 3448561432/7), zero-power hit,
and source-only saved-facing sentinel/living/dead branches.

The decisive continuation uses a dry **2x1** map, so both attack directions meet
the existing off-map-beyond-target restriction. A=(0,0), B=(1,0), both HP50, PA5,
power5; A faces West with class evade100, B faces West with class evade0. A owns
the active turn at CT0; B is ready at CT100; seed1/count0, ticks0. Execute:

- A attacks B: hit25, B HP25, six draws, word2516284547. Correct A facing=West;
  old A facing=East. Act is used, Move is open. All other state/events agree.
- A waits: CT0 to20 for unused Move, no RNG. B becomes active on Advance with
  CT100 modulo100=0, readiness raw23010, word3655513600/count7, ticks still0.
- B attacks A from the right: correct A facing West makes this a back attack,
  dropping class evade; hit25, A HP25, six more draws, word595337866/count13.
  Old A facing East is frontal: class evade100 forces a miss at the fourth roll,
  A HP50, word180171308/count11. B retains its incoming West in both policies.

This demonstrates a **later** behavioral/RNG correction. Do not demand old and
new multi-action journals stay equal when facing changes a later evade branch.
Both corrected RNG copies and replay/restored continuation must instead match
the corrected source-derived expectation.

## Fixture and save migration policy for ZOD-113

Nine sword v3 fixtures currently execute the old managed policy. Eight contain an
accepted attack with incoming actor South and expected settled East; the diagonal
rejection already keeps South. Preserve all nine original JSON files and prior
evidence byte for byte, including their IDs and source-claim history.

Use a new explicit policy ID, fft:combat/bounded-sword-settled-facing-v1, for new
executable fixtures. The existing schema-3 **shape** already represents every
needed field; a new schema is unnecessary. Update the test-only runner to validate
both policy IDs, classify the old sword-policy v3 fixtures as historical/superseded
(not-executed with an explicit facing-gap diagnostic), and execute new-policy
fixtures against the corrected default simulator. Keep old schema-2 descriptions
unexecuted. Do not normalize expected facings in memory, retain a second buggy
production algorithm, or relabel the old fixtures as newly passing.

Move executable assertions and malformed/mismatch-runner tests onto newly authored
new-policy fixtures, retaining historical inventory/hash/classification tests and
all non-facing regression coverage. Add an explicit supersession map from each
old fixture ID to its replacement. Update the direct tests that currently assert
East/attack direction, including cardinal misses. The diagonal rejection still
gets an executed new-policy regression, not just historical classification.

The existing sword save schemas 1/2 already store facing by name and restore it
verbatim through trusted definitions. The two built-in save fixtures and all three
attacks in SyntheticTwoUnitBattle.Commands are direction-aligned (East/West/East),
so **inspection predicts no changed save bytes or continuation**. Prove this in
ZOD-113 at all 13 existing checkpoint boundaries; retain built-in profile/content
versions and historical save bytes only if that check passes. No serializer shape,
transient animation field, automatic facing rewrite or new save feature is needed.

Also serialize/restore a reachable version of the two-tile misaligned-facing vector
using the existing sword API and fresh matching definitions. TwoUnitBattleDefinition
requires an AddCt start with unused choices, so the active-turn API vector above
cannot be supplied directly as its initial definition. The fixture's separate
scenarioSaveContinuation starts both units at CT90/speed10 with seed1 and no
active turn. Prepend Advance (ticks1, A CT0/B CT100, word1103527590/count1), then
Attack(A,B), Wait(A), Advance, Attack(B,A). Correct final HP25/25 and RNG
2937909499/count14 differ from old HP50/25 and RNG836760821/count12. Test all six
save/replay boundaries. At each boundary fresh matching definitions must reproduce
the corrected uninterrupted continuation. A saved facing is authoritative;
the loader cannot infer or undo a previous attack from it. Arbitrary old custom
profiles do not gain an across-version old-algorithm continuation guarantee; callers
requiring one must explicitly version their trusted profile. Do not enable ability
saves: ZOD-106 and its save/restore guards remain deferred/intact.

## Next bounded implementation

**ZOD-113:** correct the settled actor result in AttackBattleSimulator.Attack and
apply the fixture policy above. Recheck fresh decomp HEAD before translation.
Do not change target-front classification, evasion, damage, current action draws,
turn/event order, range or knockback guards. Cover the acceptance inventory in the
evidence, including probe purity, exhaustion, both action orders, KO, subsequent
evasion, existing save regression and corrected replay/restored continuation.

Run locked isolated build/full .NET and Python suites plus repeatable headless
scenarios. Verify all original fixtures/evidence and prepared content/media stay
unchanged. Rendering currently receives no facing in SceneUnit, so this ticket
does not require a new UI or native launch; reassess only if presentation changes.
Do not claim old native reports validate newly compiled code. Editor, ZOD-106 and
other documented deferrals remain in place.
