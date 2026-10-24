# Smallest ordinary CT and turn-completion slice (ZOD-019)

Date: 2026-10-22. Reference: read-only local `C:\repositories\fft_decomp`, actual HEAD **`611b504d25d4a9b03fab8023d7dbea75fe381d5d`**, observed with `git rev-parse HEAD` for this investigation. Every decomp path/symbol below is pinned to that revision. Compatibility target: North American PS1 `SCUS-94221`.

This is static source tracing and a proposed bounded scenario, not a managed implementation, executable C oracle or original-game observation. Numeric vectors below are **source-derived / source-traced**; initial unit IDs, chosen speeds and command sequences are synthetic setup. Neither this note nor a transcription of its arithmetic proves full game equivalence.

## Selected scenario and exact preconditions

Use two present, living, unmounted ordinary units, A in original battle slot 0 and B in slot 1; all remaining original slots are absent (`active_battle_id == 0xff`). Both have current Speed 10, initial CT 0 and `has_turn == 0`. Their current/inflicted status masks are empty, Brave is at least 10, their ordinary dry terrain does not inflict Poison, and they have no timed/charged actions, pending action results, scenario events or Mimic work. Movement and instantaneous action markers below are assumed to have reached their confirmed source entry points without changing speed, CT, statuses, life/presence or other turn eligibility.

These assumptions are stronger than merely excluding Haste and Slow. In particular, Brave below 10 and Defending can make start-of-turn upkeep stage an action result; deep water can restrict acting; poison terrain can create a post-turn result. Exclude all of these from the first implementation.

The proposed managed scenario IDs can be `sample:unit/a` and `sample:unit/b`. Their stable content/runtime identity is separate from the explicit compatibility tie key **0 before 1**. Do not infer the tie order from a localized name, string-ID lexical order, team or collection/hash enumeration.

## Source chain and storage

The authoritative live scheduler is `src/battle/turn_order.c::turn_run_clock`, with states/modes/events in `include/fft/turn.h`. `src/battle/action_menus.c::turn_take_next_event` calls it in RUN mode, extracts the event kind from bits 8..15 and unit slot from bits 0..7, then starts the event. `turn_start_next_event` wraps that handoff in presentation/scenario work. The AT List is a prediction path, not the live clock.

`include/fft/unit.h::unit_stats_t` stores current attributes as three unsigned bytes: PA, MA, **Speed** (`UNIT_ATTRIBUTE_SPEED == 2`). CT at offset `0x039`, `has_turn` at `0x186`, `movement_taken` at `0x187` and `action_taken` at `0x188` are each `u8`. `include/psx/types.h` defines `u8` as unsigned char, `u16` as unsigned short, and `s32`/`u32` as signed/unsigned int for this PS1 target. Source indices here address `g_unit_stats`; the original slot count 21 is an import/compatibility fact, not a recommended engine collection capacity.

`turn_run_clock(RESET)` sets clock phase ADD_CT, active scheduler unit -1, each slot's CT and `has_turn` to zero, ability outcome to zero, and charged-ability CT to `0xff`. It also clears relevant pending Mimic/Golem state. It does **not** explicitly clear `movement_taken`/`action_taken` in this reset loop; those flags are cleared when a unit actually receives its turn. The scenario's zero flags are an explicit setup assumption, not an extra reset behavior.

## Clock arithmetic, selection and continuation order

For the selected healthy units, `turn_calculate_status_flags` returns zero. Empty slots return `TURN_STATUS_CT_FROZEN`, so they gain no CT and cannot be selected. Speed-changing, incapacitating and mounted cases are outside this slice.

The ordinary path through `turn_run_clock` is:

1. **ADD_CT:** scan slots in increasing index order. For each non-frozen unit, load unsigned-byte Speed into a `u16` local. Compute `ct = unit->ct + speed` into `u16`; operands promote before addition, so an ordinary maximum sum 255 + 255 = 510 does not wrap as an 8-bit value. If the result is at least `0xff` (255), replace it with `0xfe` (254). Store to the unit's CT byte only after that cap. Haste/Slow modify the local speed but are excluded here. Enter PICK_UNIT after all eligible units have gained CT.
2. **PICK_UNIT:** initialize `u16 max_ct = 99`, `s32 best = 0xff`. Scan eligible slots ascending and replace the best only when `max_ct < unit->ct`. Thus the **largest current CT at least 100** wins; for equal CT, the earlier/lower slot wins. This is not simply first-ready-in-scan order when CT differs.
3. Before ordinary turn delivery, replace the selected unit's CT by **`max_ct % 100`**. This is modulo, not unconditional subtraction of 100. Quick CT 255 has a separate branch setting zero, excluded from this slice. A jumping candidate is reduced to 99 and selection repeats instead, also excluded.
4. Call `status_build_upkeep_action` for that unit. Under the selected assumptions it stages no result and returns zero, after consuming the RNG draw described below. Recalculate status flags. Set `has_turn = 1`, `movement_taken = 0`, `action_taken = 0`, and `transparent_removal_flag = 1`. Save resume phase **PICK_UNIT**, record the selected slot, and enter UNIT_TURN. Return `TURN_EVENT_UNIT_READY | slot` (`0x0100` for A, `0x0101` for B).
5. **UNIT_TURN:** if the active unit still has choices and is not CT-frozen, a repeated RUN call returns its unit-ready event without adding CT or redoing selection/upkeep. It is the same active turn, not a fresh scheduled turn. If the turn is over, continue through POISON_AND_REGEN, TERRAIN_POISON, REMOVE_TRANSPARENT, then MARK_MIMES/RUN_MIMES as applicable. With this scenario's empty statuses, dry ordinary terrain and no Mime work these produce no visible effects, then restore resume phase **PICK_UNIT**.
6. Therefore an already-ready peer is selected **before another ADD_CT**. Only when PICK_UNIT finds no CT >=100 does the clock traverse TICK_CHARGES, RESOLVE_CHARGE, EXPIRE_STATUSES, COLLECT_PENDING_ACTIONS and RUN_PENDING_ACTIONS. Under the exclusions none emits an event; it then returns to ADD_CT for the next clocktick.

Elapsed clockticks count ADD_CT passes, not UI frames, API calls, emitted turns or iterations of the C switch. Multiple units can take turns during one clocktick. The source's `count >= 1201` safety stop counts switch-loop steps in one RUN call, not 1201 CT ticks; do not copy it as a scheduler time unit. A bounded managed advance should report an explicit stall/limit instead of hanging when no unit can become ready.

## Move, Act and Wait completion

`turn_set_move_taken` and `turn_set_act_taken` call `turn_set_move_act_taken` with only their own flag set. That helper leaves the other flag unchanged, ignores absent slots with return -1, writes the selected flag to 1 and checks `turn_should_end`. In this status-free, unrestricted subset, `turn_should_end` becomes true only when **both** movement and action have been taken; the second marker calls `turn_end` immediately. Move then Act and Act then Move therefore both finish automatically. Taking one does not finish the turn or immediately change CT.

Caller evidence distinguishes committing a choice from merely opening a menu:

- `src/battle/state_handlers.c::move_confirm` updates the confirmed tile, sets the presentation unit's `TURN_ACTION_MOVED`, then calls `turn_set_move_taken` before post-movement events. `state_handle_move_confirm_menu` can instead undo an unconfirmed ordinary move and return to destination selection without this marker. Teleport confirmation exceptions are excluded.
- `src/battle/state_actions.c::action_execute_ability` calls `turn_set_act_taken` **before** `action_commit_preparation`. The marker is not delayed until damage resolves and is not conditional on a successful hit. The chosen scenario models only that committed instantaneous action marker; it does not invent a combat formula or assert that every rejected/preparation-failed request consumes an action. Charged/Jump actions have separate paths and are excluded.
- `src/battle/state_handlers.c::state_handle_wait_menu` advances to facing selection after confirmation, while cancel can return to after-command state. `state_handle_wait_direction` calls `turn_end` on the final player confirm, re-registers tile/facing and starts the next event. Canceling facing selection does not call `turn_end`. The AI path also eventually calls it, with presentation frame delays that are not CT ticks. The managed Wait command should represent the **confirmed end-turn decision**, not the menu/frame sequence.

Keep `unit_t.turn_action_flags` presentation/command bits distinct from `unit_stats_t.movement_taken`/`action_taken`, which drive this CT rule. For example, `state_handle_ai_command`'s end-turn path sets the former bits before calling `turn_end`; that does not mean both stats flags were taken and does not remove the unused-choice refunds.

`turn_end` is authoritative for CT completion. For a present unit with `has_turn != 0`, it loads its existing CT into `u16`, adds **20** if movement was unused and not prohibited by Don't Move, adds **20** if action was unused and not prohibited by Don't Act/Performing, then clamps the final value to **60**, writes the CT byte and clears `has_turn`. In this ordinary unrestricted subset:

`endCt = min(60, entryRemainder + (moved ? 0 : 20) + (acted ? 0 : 20))`.

No global tick or other unit's CT is changed by the completion helper. The flags are not cleared there; next readiness resets them. An absent unit or `has_turn == 0` returns 1 without applying another refund, so the completion side effect is idempotent. Auto-completion after the second marker followed by a later Wait/facing handoff must not refund a second time.

The familiar effective costs 100/80/60 for both-used/one-unused/both-unused are only shorthand where the final cap and modulo do not alter that interpretation. At high ready CT, **both-used also clamps to 60**; do not implement three fixed costs by subtracting from the original CT and omit the cap/modulo order.

## RNG side effect at new readiness

`turn_order.c::status_build_upkeep_action` clears the selected unit's action-result record and immediately executes `roll = rand()` **before** checking statuses. Even a healthy, ordinary unit with Brave >=10 consumes this draw, then returns zero. The surrounding order is **choose unit → CT modulo → upkeep draw → flags reset/turn-ready delivery**. Repeated UNIT_TURN notifications do not call this helper again. `turn_end`, the ordinary Move/Act flag markers and CT addition/selection have no direct RNG calls.

The no-status paths of `action_resolution.c::status_update_expiring`, `status_prepare_poison_and_regen`, `status_prepare_terrain_poison`, `status_prepare_transparent_removal`, and their clear-target helpers were inspected. The expiry helper calls `formula_core.c::status_modify_inflictions`; that function performs mask filtering and no random draw. For empty statuses/non-poison terrain, these clock-side paths add no draw beyond the new-readiness upkeep call. This is a statement about the traced bounded scheduler path, not all game UI, action formulas, AI simulation or movement events. Those other consumers can advance the global original stream and remain excluded.

ZOD-013's [BIOS generator evidence](bios-rng-verification.md) establishes numeric generator arithmetic separately. A managed turn-cycle implementation must explicitly thread `FftRandomState` through the upkeep selection step or document a deliberate compatibility deviation; it must not call this scenario RNG-free merely because statuses have no visible effect. The existing movement-only transition's zero-draw property remains a separate bounded claim.

## Checkable source-derived vectors

These descriptions are not movement fixture v1 records and have not been executed by a fixture runner. The current fixture format has no turn/CT command schema; version that extension deliberately before calling these executable scenarios.

**Tick and selection boundaries:**

- ADD_CT `(ct=89, speed=10)` stores 99; a sole such unit is not ready. `(90,10)` stores 100 and selection leaves 0. `(91,10)` stores 101 and selection leaves 1.
- ADD_CT `(249,5)` stores 254. `(250,5)` computes 255 and stores 254. `(254,255)` computes 509 and stores 254, not 253 from an 8-bit wrap. After ordinary selection 254 becomes remainder 54.
- At PICK_UNIT, A slot 0 at CT100 and B slot 1 at CT101 selects **B** despite its later slot. A100/B100 selects **A**. Reversing stable string names while retaining slots must not change these choices.
- Direct ordinary selection CT199 leaves 99; CT200 leaves 0; CT254 leaves 54. The multiple-100 boundary makes `%100` materially different from subtracting once. CT255 is Quick's separate case, not an ordinary tick result.

**Completion arithmetic**, with an already-selected, present active unit and no restrictions:

- Remainder 0, neither taken, confirmed Wait → CT40; only Move taken then Wait →20; only Act taken then Wait →20; both taken →0, automatically ended by the second marker.
- Remainder 19, neither taken →59; remainder 20 →60; remainder 21 →60 after the cap. With only one unused choice, remainder 39 →59 and remainder 40/41 →60.
- Remainder 60 and both taken →60; remainder 61 and both taken →60; remainder 99 and both taken →60. Thus a ready CT199 unit takes its turn at CT99 but finishes at CT60 even after both choices are used.
- Remainder 54 from selected CT254: both used →54; Move-only/Act-only/both-unused →60. After any completed case, a second `turn_end` leaves CT unchanged because `has_turn == 0`.

**One complete two-unit command/state sequence:** each transition below assumes the excluded status/charge/presentation work has no effect. Global clockticks start at zero and increase only with ADD_CT.

1. Start A/B CT `(0,0)`, Speed `(10,10)`, no active turn. After 9 ticks CT is `(90,90)` and nobody is ready. Tick **10** gives `(100,100)`; PICK_UNIT chooses A by slot tie, leaves `(0,100)`, clears A's Move/Act flags and marks A active. New-readiness upkeep draw #1 occurs.
2. A **Waits without Move or Act**. A gains40 and becomes inactive: `(40,100)`. Resume PICK_UNIT without a tick; B is chosen, CT becomes `(40,0)`, B active with clear flags; upkeep draw #2 occurs, still tick10.
3. B **confirms Move**, setting only its movement flag; CT remains `(40,0)`, B still active. B then **Waits**, gaining20 for its unused Act; `(40,20)`, no active turn.
4. No peer is ready. Six further ticks lead to tick **16**, CT `(100,80)`; choose A and leave `(0,80)`, A active with clear flags, upkeep draw #3.
5. A **confirms Move then commits Act**. The Move marker alone leaves CT unchanged and A active; the Act marker makes both flags1 and automatically ends A. CT `(0,80)`, no refund, no additional tick. A later completion handoff must leave this unchanged.
6. B is not ready at80, so two more ticks reach tick **18**, CT `(20,100)`; choose B, leave `(20,0)`, clear its flags and mark active; upkeep draw #4.
7. B **commits Act only**, leaving its Move available and CT unchanged, then **Waits**; gain20 for unused Move, yielding `(20,20)`, neither active.
8. Eight further ticks reach tick **26**, CT `(100,100)`; choose A again by the same slot tie, leave `(0,100)` and mark it active; upkeep draw #5. Stop here. B remains ready and must be considered before a further tick once A ends.

The five draws above count only the bounded clock-side upkeep selections. They do not assert a total original-game draw count for performing actual Move/Act commands, animation/UI or AI. For a scheduler-only regression using synthetic flag commands with no additional effects, those five draws can be asserted explicitly. Replaying from identical CT, speeds, slot keys, flags, clock phase and RNG snapshot must reproduce the same five selections and state changes.

## Exclusions, quirks and remaining evidence limits

- No Haste/Slow, Quick/CT255, Sleep/Stop/freeze-CT statuses, Petrify, death/crystals/treasure, Reraise/undead revival, Jump, Death Sentence, Brave<10/Chicken upkeep, Defending removal, poison/regen, Transparent, status expiry effects or terrain status effects.
- No charged abilities, song/dance/Performing, Mimic, reactions/counters, forced scenario/event turns, presence changes, speed/CT-changing actions, mounts/riders or deep-water action restrictions. No movement undo/teleport exception, preview or AI end-turn simulation is modeled.
- No AT List implementation. `turn_order.c::at_list_build` substitutes Speed1 when its source Speed is0, while the live ADD_CT loop does **not**. Reusing prediction arithmetic as the live scheduler would silently change zero-speed behavior. For this first slice require positive byte-range Speed; leave zero-speed/no-progress behavior explicit rather than normalizing it to1.
- Fixed21 slots, 1201 switch steps, packed event IDs and UI facing/frame delays are not generic engine requirements. The CT threshold100, tick cap254, slot tie order, modulo update, refund20/cap60 and upkeep RNG draw are explicit FFT compatibility policies.
- No C harness, BIOS/game execution or emulator capture of this turn cycle was performed. The source paths establish a narrow source-derived expectation; source table status-set assumptions and complete runtime integration still need later executable/original comparison.

## Scheduler seam recommendation

Implement ZOD-020 first as a small FFT-owned explicit transition: ordered eligible units with stable runtime IDs and explicit tie keys, per-unit CT/current Speed/turn flags, active unit, clock/resume phase, and explicit RNG state. Commands should distinguish confirmed Move/Act consumption from confirmed Wait. Return an updated immutable state and ordered tick/readiness/completion events. A legal command must not mutate its input; rejected requests need an explicitly tested no-change/no-draw policy rather than a claim about every original UI rejection path.

The engine-neutral seam is **advancing explicit scheduler state until an ordered ready candidate or an explicit no-progress result**, with game-supplied progression, ordering and completion rules. Zodiac can own that deterministic transition boundary if implementation demonstrates a reusable primitive; it must not own CT100, byte caps, refunds, PS1 slot interpretation or the upkeep call. For this first slice it is reasonable to keep all scheduler arithmetic in `Fft.Core` and reuse the existing neutral explicit-state/RNG facilities rather than create an empty universal scheduler framework.

At the FFT-to-Zodiac seam, preserve the ordered candidate tie key and full explicit state. Selection must invoke FFT's upkeep RNG policy at the traced point before ready delivery. A second game can choose another readiness scale or completion policy without importing any of these constants. This follows [ADR0004](../adr/0004-zodiac-engine-and-fft-game-boundary.md) and the [architecture overview](../architecture/overview.md); no project or namespace changes are required by this discovery.

## Validation performed

Inspected the live scheduler, its source callers, status restrictions/empty-status helpers, storage-width declarations, turn flags and completion helpers at the pinned revision. Cross-checked threshold/overflow/modulo/refund boundaries and the tick10/16/18/26 sequence by arithmetic. Source paths plus symbols and the full SHA identify evidence; line numbers are not the reference identity. Relative documentation links and whitespace were checked. No production files, fixtures, tests, BINs or game assets were changed; no .NET tests were needed for this documentation-only ticket.
