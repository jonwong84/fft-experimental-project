# ZOD-108 — Throw Stone source/data contract

Date: 2026-10-26. Managed base: 119cb73 (ZOD-107). Working tree was clean on
entry; owner approval documentation was then recorded. Read-only reference
C:\repositories\fft_decomp was freshly checked clean at
11464943bbdbd2a0af022d90cb7f10455cf911df. Every reference path/symbol below
refers to that revision.

Select **Throw Stone**, ability 148 (0x94), under ZOD-038. It adds targeted
damage and the first bounded forced movement to the Basic Skill lane.
This completes a source/data contract, not a managed action or native
presentation. The [evidence/acceptance inventory](../evidence/combat/throw-stone-contract-v1.json)
separates completed offline checks from 34 unexecuted implementation cases.
Accumulate's approved authored controls are complete. Editor work and ZOD-106
battle checkpoints remain deferred.

## Verified records

The prepared SCUS_942.21 is 356,352 bytes, SHA-256
287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562.
Its PS-X EXE header gives load address 0x80010000, payload 0x56800,
and file header 0x800. Reads use address minus load address plus header,
confined to that verified payload. No BIN or original executable is launched.

Two offline audits repeat byte-for-byte. They recheck all 22 historical imported
metadata records and these six selected records against the prepared bytes:

- g_job_data[74]: file 0x52698, 48 bytes; generic Squire selects skillset 5.
- g_ability_human_skillset_abilities[5]: file 0x55311, 25 bytes; action slot 2 is 148.
- g_ability_data[148]: file 0x4f890, 8 bytes; JP 90, learn-on-hit rate 80.
- g_ability_range_data[148]: file 0x50c08, 14 bytes.
- g_command_menu_types_by_skillset[5]: file 0x564b9, one byte; default menu.
- g_status_infliction_data[0]: file 0x547c4, six bytes; empty.

The secondary record specifies range 4, area 0, vertical 0, Cannot Target Self,
Cannot Hit Caster, No Follow Target, Evadeable and Direct Targeting; remaining
flags and element are zero. Formula 0x37, X 0, Y 2, status index 0, charge 0, MP 0.
Primary classification/AI bytes and raw records remain in ignored audit output.
AI/learning metadata is not an executable effect or learned grant.
target/main.yaml and include/fft/ability.h supply addresses, strides and flags.
Historical import provenance is preserved.

## Selection, range and actual hit target

menu/ability_choices.c::skillset_get_unit_ability_data derives learned ordinary
actions and menu restrictions. Authored actors need explicit grants validated
against job/skillset/learning records; listing Throw Stone grants nothing.
The self-action command cannot supply a target. Add a separate typed
actor/ability/target command without changing that command's meaning.

combat/targets.c::target_range_set_for_action selects ability_range_set for the
default menu. panels_spread / panel_to_neighbors_spread propagate an orthogonal
budget; on an open flat map the selectable distance is Manhattan 1..4. Diagonals
are allowed within that total. Cannot Target Self clears the caster's panel.
The vertical-tolerance flag is absent: vertical 0 is **not** an enabled zero-height
tolerance test. No Follow Target selects a tile, rather than a charging action
following a moving unit. The proposed unit-target command is a deliberate subset
for immediate actions on occupied panels; empty tiles are outside this first API.

Direct Targeting does not bypass obstruction checks.
preparation.c::action_tiles_calculate routes it through target_validate_for_weapon.
Throw Stone lacks Weapon Range/Weapon Strike, so that function selects
WEAPON_FLAG_DIRECT, not the equipped sword's reach or an arc.
effects/projectile.c::projectile_direct_check_to_target traces raised unit centers
and may return an intervening unit; direct_path_trace tests unit/terrain obstacles.
General projectile behavior cannot be replaced with Manhattan range alone.

The first managed profile admits only two ordinary, unmounted units on one open,
dry, flat lower-layer rectangle at height 10, with ordinary positive body height,
no blocked/untargetable/nonstoppable cells, water, slopes, ceilings, terrain
obstacles or extra intervening units. The projectile between occupied panels is
clear in this domain. Reject unsupported setup before drawing or spending Act.
Do not claim arbitrary terrain, vertical targeting, interception or empty-tile use.
Recheck Manhattan 1..4 after every move or push.

## Formula, widths and draws

resolution.c::action_calculate_target_result loads the default ability's secondary
formula/status record, drops its weapon, and dispatches s_formulas[0x37 - 1] to
formulas.c::formula_damage_random_y_times_pa. It performs physical evasion, then
stores effective PA/Y, applies Attack Up/Martial Arts, Berserk/Frog, Defense Up,
target physical statuses and zodiac, and calls
formula_core.c::formula_apply_damage_and_knockback.

Retain the existing explicit restrictions: PA 1..99, Brave 10..100, same-sign
neutral zodiac, zero MP, equipment PA bonus and equipment evade, no supports,
reactions, weather modifiers, mounts, affinities or status modifiers beyond
HP-derived Critical/Dead. Use current effective PA, including an earlier Accumulate.
XA is signed 16-bit; damage is stored signed 16-bit after 32-bit working arithmetic
and an unsigned-16 cast of XA. This domain bounds damage to 198, avoiding overflow
or truncation ambiguity. Broader modifiers require another trace.

Physical evasion goes through formula_modifiers.c::formula_evade_calculate_physical
and formula_core.c::formula_evade_store_physical / formula_evade_calculate_facing /
formula_evade_calculate_final_hit_percent / evade_source_roll:

1. Roll accessory, right hand, left hand, then class, including zero-valued sources.
   The first profile has zero equipment evades.
2. Class evade applies only from the front. With a dominant horizontal/vertical
   offset compare the corresponding opposing facing. On an exact diagonal,
   either facing toward the actor counts as front. Side/back clear class evade.
3. Front class evade 100 still draws four times and misses. A miss consumes Act
   but skips the remaining formula draws. No critical/proc draw is added.
4. A hit draws damage = (floor(raw * 2 / 32768) + 1) * PA.
   Raw 0..16383 gives PA; 16384..32767 gives 2 * PA.
   PA 5 gives 5/10 damage; PA 6 gives 6/12.
5. The next formula_get_random draw gives
   threshold = floor(rawAttacker * attackerBrave / 32768).
6. main/unit_state.c::random_roll_pass_fail independently draws and attempts
   knockback when floor(rawTarget * targetBrave / 32768) < threshold.
   Equality fails. This is not a fixed 50% chance or a comparison of raw rolls.

An admitted hit uses **seven** resolution draws; an evaded action uses **four**.
Both knockback draws occur even if movement later fails at an edge or is canceled
by lethal damage. There is no weapon critical or 19% status-proc draw. Existing
turn readiness/upkeep draws remain separate.
outcomes.c::formula_get_random returns midpoint 0x4000 for preview/AI, while
random_roll_pass_fail itself uses rand; do not call the original preview flow pure.
The managed API instead probes copied immutable RNG/state and discards results,
or uses a separately named deterministic projection. Neither mutates live state.

The supplied BIOS evidence already verifies the project's RNG recurrence; it does
not establish this new action's ordering. The audit's arithmetic vectors are
source-derived Python calculations, not managed tests, C execution, original
observations or whole-stream equivalence.

## Knockback and settled state

formula_core.c::knockback_calculate requires one target and excludes self,
immortal, importance-3 and mounted targets. It moves one tile away along the
dominant axis, with **Y winning a diagonal tie**. Off-map, occupied, rising/
obstructed, water, steep, lava and obstacle destinations can cancel movement.
The source selects terrain layers/fall height; the first flat profile implements
neither falling nor layer selection. On its open rectangle an in-bounds destination
is an empty, flat, dry ordinary tile; off-map suppresses the push, retaining damage.

resolution.c::action_finalize_target_result caps positive damage at 999 and
cancels knockback when damage >= current HP. The seven draws already occurred.
Apply HP loss, HP-derived Critical/Dead and byte KO-count increment as in the
bounded lifecycle. First KO completes the authored scenario. No death scheduler,
revival, EXP/JP or campaign transfer is added.

preparation.c::action_resolve_strike schedules a pending push as a follow-up
ABILITY_ID_KNOCKBACK phase. units/animation_policy.c::action_start_target_response
starts units/movement.c::move_knockback_start / move_knockback_update;
move_settle_on_tile stores the new position. resolution.c::knockback_apply adds
no fall damage for a same-height landing. The managed atomic command collapses
damage and final displacement into one settled result; visual timing is separate.
Preserve target voluntary Move/Act flags, CT, speed and facing; forced movement
does not spend its choices.

A relevant facing distinction was found. controller.c::action_apply_strike_results
temporarily turns the actor through unit_facing_turn_to_target, which saves
attack_facing. reward_grant calls facing_all_after_attack_restore; living units
regain saved facing. action_store_actor_data stores position/mimic data and does
not defeat restoration. The new settled action retains actor and target facing.
The current authored sword action's persistent actor-facing change is an identified
ZOD-038 fidelity gap, documented separately and left unchanged here. Do not reuse
it as a compatibility rule. No animation-facing or reward implementation is added.

## Managed boundary and delivery

Use ordinary named C# arithmetic (for example ThrowStoneFormula) in the FFT rule
area; no formula scripts, generic effect language, Engine interface or editor work.
Keep old self-effect/sword definitions and commands intact. Add a separately
selected closed targeted-effect definition/profile and immutable explicit grants.
Simulation references neither content parsing, files, rendering nor C code.
Content projection validates the exact selected effect and shared references.

Before drawing validate actor/target, active turn, unused Act, living distinct
units, selected effect/grant, range and supported setup. Rejections, including
draw-counter exhaustion, retain the exact input snapshot with no events/RNG changes.
Acceptance marks Act through the existing turn transition, then publishes RNG,
HP/status and optional displacement atomically. Leave unused Move available.
Use turn events first, a named targeted-action result and optional HP/status/KO
and forced-position events; document exact event order in implementation.
Distinguish a knockback attempt from actual displacement in diagnostics.
Replay rejected and accepted commands; synchronize both existing RNG copies.

1. **ZOD-109 — pure targeted-action proof**, TST-001..020: named arithmetic,
   explicit closed rules/grants, immutable damage/flat knockback, turn/RNG/state
   continuation and regressions. No content import, native UI or persistence.
2. **ZOD-110 — validated content/import and authored scenario**, TST-021..028:
   separate targeted companion, metadata/grant projection, verified six-record
   offline conversion, repeated import and preserved metadata. Explicitly opt into
   Accumulate plus Throw Stone and prove boosted PA feeds ranged damage. Reuse the
   first-KO lifecycle and retain old sword domain guards. Ability-enabled saves
   keep failing explicitly before file operations; ZOD-106 remains deferred.
3. **ZOD-111 — prepared Windows targeted controls**, TST-029..034: write a finite
   native plan after pure/content proofs; add target/confirm/cancel, range feedback
   and HP/PA/position results through shared commands. Repeat headless/replay/
   capture checks and retain a separate owner approval gate.

All 34 acceptance cases are unexecuted. No runtime, schemas, tests or native
launches change here. Last implementation validation remains 2,459 .NET / 299
Python tests from ZOD-107; these suites were not rerun for this contract.
