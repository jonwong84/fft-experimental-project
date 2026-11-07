# Reference refresh before ZOD-022

Date: 2026-10-22. Target: North American PS1 `SCUS-94221`.
Reference checkout: `C:\repositories\fft_decomp`, read-only during this review.

- Previous evidence revision: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`.
- Updated HEAD, verified with `git rev-parse HEAD`: `11464943bbdbd2a0af022d90cb7f10455cf911df`.
- The reference checkout and managed project's working tree were clean at the start.
- Managed project baseline: `1caa692d1cc00b5dce8c7cc2e1dc043c5b63a99c`.

This is a targeted source review of the attack/setup assumptions needed by ZOD-022,
not a review of all 225 intervening commits or an executing original-game oracle.
No change requiring adjustment to the existing bounded synthetic attack vectors was
identified. The compatibility target remains the retail game; upstream organization
and identifier choices do not become new gameplay behavior.

## Current navigation and identifiers

All current paths and symbols in this note refer to the updated HEAD above.
Historical paths remain valid at their historical commits.

- `src/battle/formula_weapons.c` attack dispatch is now in
  `src/battle/combat/formulas.c::formula_weapon_damage`.
- `src/battle/formula_effects.c` helpers are now in
  `src/battle/combat/formula_modifiers.c`, including
  `formula_calculate_weapon_damage`, `formula_evade_calculate_physical`,
  `formula_roll_conditional_status_proc`, `formula_apply_status` and
  `formula_apply_status_to_action`.
- `src/battle/formula_core.c` is now `src/battle/combat/formula_core.c`.
  `formula_evade_roll_source` is now `evade_source_roll`;
  `move_knockback_calculate` and `move_knockback_check_destination` are now
  `knockback_calculate` and `knockback_destination_check`.
- `src/battle/action_resolution.c` is now `src/battle/combat/resolution.c`.
  `action_calculate_target_result`, `action_finalize_target_result` and
  `action_apply_target_result` retain their names.
- `src/battle/action_preparation.c` is now `src/battle/combat/preparation.c`.
  `unit_calculate_height_data` is now `unit_action_height_calculate`.
- `src/battle/target_panels.c` is now `src/battle/combat/targets.c`.
  `target_range_calculate_weapon` is now `range_weapon_calculate`;
  `target_range_calculate_strike_lunge` is now `range_strike_lunge_calculate`;
  `target_range_mark_tile_in_height_band` is now `range_tile_in_height_band_mark`;
  `target_validate_height_overlap` is now `height_overlap_validate`.
- Action execution, facing and strike application formerly in
  `src/battle/state_actions.c` are now in `src/battle/turn/controller.c`.
  `action_execute_ability` and `action_apply_strike_results` are file-local.
- `src/battle/turn_order.c` is now `src/battle/turn/clock.c`.
  `turn_set_act_taken` is now `turn_act_taken_set`;
  `turn_set_move_act_taken` is now `turn_move_act_taken_set`.
- `src/battle/action_outcomes.c` RNG helpers are now in
  `src/battle/combat/outcomes.c::formula_get_random` and
  `formula_roll_random_at_least`. `random_roll_pass_fail` formerly in
  `src/main/unit_growth.c` is now in `src/main/unit_state.c`.
- Equipment assembly formerly in `src/main/party.c` is now in
  `src/main/unit_init.c::unit_equipment_stats_set` and
  `unit_equipment_attributes_set`.

`g_action_current_ability` / `action_current_ability_t` are now
`g_action_work` / `action_work_t`. In `include/fft/action.h`, the four contiguous
evade bytes are grouped as `action_evades_t evades`: accessory, right_hand,
left_hand and job at offsets 0x1e..0x21. XA/YA remain signed 16-bit at 0x0e/0x10;
the work record retains its 80-byte size assertion. These are C representation
changes to navigate, not a reason to reproduce its packed records in managed state.

## Comparison method and changes requiring review

A read-only `git show` comparison extracted 17 relevant function definitions from
both commits. Fourteen matched after removing comments/whitespace and normalizing
the inspected action-work/evade field and helper identifier renames: weapon-damage
calculation, empty status application and its wrapper, proc gating, physical evade
coordination/storage/facing/final rolls, the individual evade roll, base XA/YA,
damage multiplication, action marking and the turn-end predicate.

Three needed manual review rather than textual normalization:

- `formula_weapon_damage` collapses nested conditions into `a == 0 && b == 0`.
  C short-circuit evaluation retains evade before damage and skips damage on evade.
- `formula_calculate_critical_hit` calls renamed `knockback_calculate` after the
  same critical check and bonus calculation. There is no extra random draw in
  that rename. The selected destination remains out of bounds and is rejected
  before terrain processing by `knockback_destination_check`.
- `turn_end` removes a temporary `u32 result` that tracked the incremented `u16 ct`.
  It compares `ct` directly with the same cap. Starting with a byte CT and at most
  two refunds of 20 cannot overflow the intermediate 16-bit value; this preserves
  the bounded refund/cap behavior. Constants remain refund 20 and cap 60.

This normalization is a review aid, not a semantic proof. Targeting, result
application, caller ordering, widths, constants and selected equipment paths were
also inspected directly at the new paths below. Behavior outside the stated
profile was not certified by these comparisons.

## Rechecked bounded behavior

Targeting still selects weapon formula 1 for Attack rather than ability 0's
secondary formula (`combat/resolution.c::action_calculate_target_result`). Striking
still marks the four cardinal neighbors in the inclusive half-height band
`actorHeight - 6 .. actorHeight + 5`. Body validation retains the asymmetric
`targetWalking < actorTotal && targetTotal >= actorWalking` comparisons. Ordinary
body height is six half-height units; Dead makes height unavailable in
`unit_action_height_calculate`. `target_validate_for_weapon` still reads the
actor's stored target position and may return 0 without marking a valid target;
its return value alone is not an attack-validity check.

Physical evade retains the same weapon/Weapon Guard/unrestricted-action gate.
The schema 2 sword's raw evade therefore remains distinct from effective hand
evade in the no-reaction setup. Class evade clears for side/back and hand evade
also clears for back. The four evade checks remain ordered accessory, right hand,
left hand, class; zero chances still draw and successful evasion exits early.

`unit_state.c::random_roll_pass_fail` remains multiply/divide scaling of a BIOS
draw by 32768, comparing `roll >= threshold`. The formula RNG helper still returns
the fixed midpoint outside execution and calls `rand()` during execution.
`include/fft/formula.h` still defines critical chance 4 and proc chance 19.
The critical bonus remains `baseXA + raw * baseXA / 32768`; damage multiplication
still narrows into signed 16-bit. Empty type-0 and type-128 proc paths retain their
different encodings and add no status-selection draws. The conditional proc roll
still precedes empty status application after a successful neutral hit.

`action_finalize_target_result` still caps positive damage at 999 and cancels
pending knockback/proc when damage is lethal. `action_apply_target_result` still
returns without HP mutation on a miss, otherwise calculates signed 32-bit
`oldHP - damage + healing`, clamps to 0..maxHP, applies Dead at newly zero HP,
and increments the unsigned-byte KO counter in execution. `include/fft/unit.h`
retains signed-16 damage, unsigned-16 HP and unsigned-byte KO count. A future
implementation must retain byte conversion at KO-count overflow. Nonzero HP at
or below integer `maxHP / 5` still selects Critical.

`turn/controller.c::action_execute_ability` still marks action taken before
committing preparation/resolving results and turns the actor toward the cursor.
The turn ends when both choices are unavailable; an attack alone leaves unused
movement available. Refunds remain 20 per unused unrestricted choice, capped at 60.

The checked synthetic vectors therefore remain normal hit seed 1: damage 25,
six draws, outgoing state 2516284547; frontal miss seed 19: four draws, state
2162199151; critical seed 57: damage 45, seven draws, state 3448561432. This
review checks their source assumptions; it does not execute those combined FFT
formula vectors or turn them into original-game observations.

## Import and generator evidence policy

`target/main.yaml` retains the selected table addresses: ability 0x8005ebf0,
ability range 0x8005fbf0, primary item 0x80062eb8, weapon 0x80063ab8, status
infliction 0x80063fc4, item attributes 0x800642c4 and menu-by-skillset 0x80065cb4.
`unit_equipment_stats_set` still copies raw weapon power/evade; physical combat
then separately gates weapon evade as described above. This does not establish
full equipment-derived stats or imported retail battle equivalence.

The `rand`/`srand` files under `src/platform/psyq/libapi/` remain BIOS tail-call
veneers. They do not supply a newly recovered RNG implementation. The independently
verified BIOS recurrence and its existing evidence remain unchanged.

Keep prior analyses, fixture source references, importer provenance and code
comments pinned to the commits actually used to create them. Do not globally
replace their SHAs or regenerate original content merely because the reference
checkout advanced. New ZOD-022 implementation notes and new source-derived fixtures
should cite the updated revision and current symbols, linking the historical
[attack trace](ordinary-physical-attack.md) and this bridge note as appropriate.

ZOD-022 remains the next implementation ticket. Its atomic rejection/event API is
a managed contract; the source review does not certify unsupported reactions,
knockback destinations, death scheduling, rewards, full equipment or global retail
RNG-stream integration. This refresh changes documentation only; no game code or
fixture expectations were changed, and no build/test run was needed for it.
