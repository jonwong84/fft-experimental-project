# ZOD-114 — Flat sword critical knockback source contract

Date: 2026-10-26. Edition: SCUS-94221. Managed base:
99a39f722c62b9f0d7c1a0ebc8469fd9edf22fc2; working tree clean on entry.
Read-only reference C:/repositories/fft_decomp was freshly checked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df. All source paths and symbols below
refer to that revision. This is a source/description contract, not implemented
knockback or an original-game observation.

An ordinary sword critical directly attempts knockback. It does **not** use
Throw Stone's two Brave rolls. A strict open-ground, two-unit extension can
admit interior attacks without changing current-action damage or draw counts.
Keep the existing edge-only entry point until that extension is implemented.

The [evidence](../evidence/combat/sword-knockback-contract-v1.json) fingerprints
17 reference files/41 functions and 42 preserved managed/historical files.
The separate [description fixture](../fixtures/behavior/sword-flat-knockback-contract-v1.json)
contains 35 offline vectors and SKB-001..020 implementation cases, all unexecuted.
It is not an executable schema-3 behavior fixture and must not be admitted by
the current managed runner as if production supported this rule.

## Source path, data and exact order

1. combat/resolution.c::action_calculate_target_result selects the ordinary
   Attack menu's weapon formula and status record. This contract retains the
   existing validated BoundedSwordWeapon meaning: ordinary sword, range 1,
   Striking, formula 1, neutral element, empty type-0 or type-128 status set.
   See [existing executable sword boundary](../behavior/bounded-sword-implementation.md)
   and [content definition](zod-055-sword-content.md). No new retail record or
   raw import field is needed; this investigation does not re-import assets.
2. combat/formulas.c::formula_weapon_damage calls physical evade before weapon
   damage. The existing four accessory/right/left/class checks remain ordered,
   including draws for zero equipment evades. Only class can evade in this
   profile; it applies from the front. A miss skips all remaining formula draws.
3. combat/formula_modifiers.c::formula_calculate_weapon_damage calls base-XA,
   charge, element-strengthen, physical damage, element modification/absorption,
   then conditional status proc. The closed profile excludes every modifier
   except same-sign neutral zodiac and its floor to XA=1. In formula_core.c,
   formula_calculate_base_xa selects PA for ordinary swords; knight swords and
   katanas are different branches and remain excluded.
4. formula_calculate_physical_damage applies physical modifiers, calls
   formula_core.c::formula_calculate_critical_hit, then stores XA*YA damage.
   outcomes.c::formula_roll_random_at_least(100, 4) gives a critical when
   floor(raw*100/32768) < 4. This means raw 0..1310, with 1311 the first
   noncritical raw value; the discrete probability is 1311/32768.
5. On critical, one bonus draw gives XA = baseXA + floor(rawBonus*baseXA/32768),
   then **knockback_calculate is called immediately**, before damage storage,
   proc and lethal finalization. There is no extra push-chance or Brave draw.
   Normal hits never call this critical knockback path.
6. formula_store_xa_times_ya_damage stores a signed-16 product. Retain the
   existing pre-draw envelope: (2*max(1,PA)-1)*power <= 32767. PA is byte-range
   0..255, power 0..255. Finalization caps positive stored damage at 999;
   applying the cap before the signed-16 gate would admit unsupported overflow.
7. formula_modifiers.c::conditional_status_proc_roll_inner always draws the
   conditional 19-percent check during execution, even for an empty status set.
   An empty set adds no selection draws. formula_apply_status saves/restores
   hit/accuracy/miss around formula_apply_status_to_action. Its forced empty-set
   miss helper does not cancel pending knockback. Thus a successful empty proc
   does not suppress a critical push. Formula-2 weapon spells explicitly call
   formula_queue_weapon_spell and cancel knockback; they remain excluded.
8. resolution.c::action_finalize_target_result caps positive damage, then
   cancels pending knockback when damage >= current HP. Both formula draws
   and the proc draw already occurred. HP application clamps to zero and
   projects Dead/KO as before. Damage 0 does not cancel a living target's push.

Resolution budgets remain **miss 4, normal hit 6, critical hit 7**. Off-map,
lethal, zero-power and successful pushes use the same critical seven draws.
The formula helpers return midpoint 0x4000 outside execution; that is not the
managed pure-probe contract and must not be generalized to the later AI port.
No original whole-stream or presentation RNG equivalence is established here.

## Eligibility, destination and representation

formula_core.c::knockback_calculate first requires exactly one target, then
rejects immortal, importance-3, self, mounted and zero-displacement targets.
These are original semantic exclusions, not a Brave threshold. The closed
managed ordinary-unit projection must explicitly exclude them; it has no raw
team/importance/mount fields to infer them from.

The destination is one tile away from the actor along the dominant axis;
Y wins a diagonal tie in the general helper. This sword extension admits only
cardinal adjacency, so the candidate is target + (target - actor).
knockback_destination_check rejects off-map before indexing terrain. It compares
facing-edge heights on the two original terrain layers, rules out blocked/rising
destinations or insufficient headroom, selects the higher usable layer, and
rejects occupancy. target_find_unit_at_tile uses unit_find_at_tile with no
status filter: a dead occupant is not automatically empty. No third unit is
admitted by the proposed extension; occupancy is a defensive check only.

After destination selection, knockback_calculate rejects untargetable tiles,
any water depth, half-height >=3, lava and obstacles. It then stores pending
kind, target tile, fall height and the knockback special effect. It does not
consult the voluntary cannot-stop bit here. Our stricter open profile rejects
cannot-stop everywhere to keep the admitted domain simple, not to claim this
is an original forced-movement restriction.

In the proposed domain every lower tile is ordinary ground at height 10,
zero half-height/slope/water/ceiling, with no movement flags. The original
absent upper layer is blocked: its sentinel height is 255, leaving the required
four half-units above the height-20 lower edge. Selected lower destination has
fall height zero. The managed map can therefore expose one layer; no original
fixed terrain table size needs to enter the new API. The original fixed limits
remain reference-format details, not a general permission to support layering.

include/fft/action.h stores XA/YA as s16; action_data in unit.h stores hp_damage
as s16 and special_effect as u16. Knockback flags and fall height are u8;
move.h defines pending=0x80, kind mask=0x7f and grounded fall-damage kind=1.
Destination coordinates/layer are packed bytes in the source movement record;
the bounded managed API should use its existing validated TilePosition type.
No unchecked source-angle or coordinate casts are needed for this flat result.

## Follow-up phase and settled state

preparation.c::action_resolve_strike copies a pending destination into the strike
work and continues the action without counting another weapon strike. On the
next result calculation, resolution.c::action_calculate_target_result takes
the early knockback_apply branch instead of re-running the weapon formula.
That helper clears pending but retains the kind. At zero fall height it adds
no damage, and does not draw another critical/proc or consume another Act.

turn/controller.c::action_start_next_strike dispatches ordinary Attack and
ABILITY_ID_KNOCKBACK directly to action_apply_strike_results. In
units/animation_policy.c::action_start_target_response, the knockback special
effect starts movement.c::move_knockback_start. That copies the selected tile
and creates a one-step path through knockback_init / move_knockback_update.
move_step_start_walk deliberately keeps facing/animation while the battle state
is ACTION_EXECUTE. move_settle_on_tile publishes the final coordinates/facing
via placement.c::unit_set_tile_position. These helpers do not set target
movement_taken/action_taken or change its CT. The weapon actor's Act was marked
once by controller.c::action_execute_ability / clock.c::turn_act_taken_set.

Retain both units' incoming settled facings. Flat knockback does not turn the
target to face travel. The ZOD-112/113 living-actor facing restoration still
occurs through reward_grant / facing_all_after_attack_restore. That restoration
uses current map coordinates, so it does not undo the new target position.
Lethal cancellation prevents moving a newly dead target. Do not generalize this
to dead-facing restoration, falls, mounts or reaction chains.

The pure command may collapse damage plus the zero-damage follow-up into one
settled result. Preserve target HasMoved, voluntary Move/Act, CT, speed and
movement profile. Recalculate subsequent attack/movement range from its new
position. Keep the current turn-event, attack-resolved, optional HP/status/KO
ordering, then append a sword-specific forced-position event only on actual
displacement. That event shape/order is a proposed managed contract, not a
serialized original event. Do not emit a voluntary Move or a temporary facing
event for this displacement.

## Offline vectors and implementation recommendation

The description has 16 interior critical vectors (four directions by four
actor facings), four off-map edges and 15 formula/HP/Brave/proc/budget variants.
Target facing is explicit; side-hit covers suppressed class evade. Future
SKB-002 expands target-facing combinations in managed tests.

- Seed 57, PA5/power5, HP50: raws
  9511,10768,17155,21975,1171,28206,19852. Critical XA9 gives damage45, HP5,
  one-tile push when clear; final word3448561432/count7. Four edge cases keep
  position and the identical stream. HP45/44 cancel the push; HP46 pushes to HP1.
- The same critical with power0 does zero damage and still pushes. PA0 floors
  XA to1, gives damage5 and pushes. Swapping Brave10/100 to100/10 leaves all
  results and draws unchanged.
- Seed233 gives critical damage25 and a successful empty proc (last raw565),
  still pushing; final word2184573576/count7. It tests a distinct cancellation risk.
- Seed1 hits25 without pushing, word2516284547/count6. Seed57 with front
  class evade100 misses at draw4, word3587663605; facing away preserves the hit.
- Raw1310/1311 bracket the critical check. PA5 bonus raw6553/6554 brackets XA5/6;
  raw0/32767 gives XA5/9. PA255/power64 worst product32576 is admitted;
  power65 worst33085 is rejected. Accepted positive damage still caps at999.
- Exact remaining budgets 4/6/7 can finish at ulong.MaxValue. Insufficient
  budgets must reject atomically; the offline calculator checks exhaustion but
  is not evidence that the future managed command already does so.

Implement **ZOD-115**, a pure opt-in flat-sword profile with ordinary-ground
certification, exactly two ordinary units and the stricter single-layer open
rectangle validator. Preserve the current edge-only entry point/default, fixtures
and save semantics. Do not simply delete the beyond-target guard: existing
AttackBattleSnapshot.ValidateSupportedGeometry permits blocked cells, blocked
upper layers, and arbitrary terrain surface IDs. Its shape is not certification
that every destination is ordinary open ground.

Reuse settled immutable state/application conventions and, if useful, the small
flat-destination helper from Throw Stone. Do not share its Brave formula or its
seven-draw hit budget. Retain the existing sword 4/6/7 branches, signed envelope,
empty proc and facing behavior. Validate setup before draws; publish damage,
Act, displacement, events and both RNG copies atomically. Keep old edge behavior
as the default supported contract, without a second differing damage algorithm.

SKB-001..020 specify source vectors, guards, exact draw exhaustion, choices,
event order, replay/pure probes, later range, and historical regressions. All
remain unexecuted. Content/scenario/native opt-in integration can follow in its
own bounded milestone. No new save format, checkpoint feature or editor UI is
needed for this pure proof. After the attack extension is validated, select the
bounded original-AI Move/Attack/Wait source/interface mapping discussed with the
owner; retain the replaceable original/default versus explicitly custom policies.

## Verification and limits

Run from the repository root, with Python 3 and output paths that do not exist:

~~~powershell
python -m unittest discover -s tools/verification -p test_sword_knockback_contract.py -v
python tools/verification/verify_sword_knockback_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/sword-knockback-114-audit-final-a.json
python tools/verification/verify_sword_knockback_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/sword-knockback-114-audit-final-b.json
~~~

The reference argument is optional; CI/tests validate fixtures/history without
needing the checkout. The argument adds read-only clean-HEAD, normalized UTF-8/LF
file/function fingerprint checks, never linking or executing reference code.
History fingerprints use the same explicit encoding so Windows and Linux Git
newline policies do not change the preservation result.
The opt-in managed-baseline check covers eight files at this source checkpoint;
ordinary tests check the 34 historical files, allowing later deliberate managed
implementation changes without rewriting this historical source evidence.

Both offline reports are byte-identical (SHA-256
48357b281a17cf6dbe1e73c733f2ac19af88bf53845fa59371f6e2cceb28a529),
checking 35 vectors, 20 unexecuted cases, 42 preserved files and all 17 reference
files/41 functions. All **329 Python tests pass** (12 new). A mistyped expected
fourth RNG word in an initial test was corrected after an independent closed-form
recurrence check; the full suite then passed. Last full implementation validation
remains 2,804 .NET / 317 Python; .NET was not rerun for this source-only milestone.
No original/emulator/native launch or
prepared-content change. No general terrain/fall/layer semantics, extra units,
weapon spells/status procs, reactions, rewards, AI, animation or persistence
support is implied by the offline contract.
