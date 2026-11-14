# FFT RNG state and bounded attack draw ordering (ZOD-005)

Date: 2026-10-22. Compatibility target: North American PS1 `SCUS-94221`. Reference: read-only `C:\repositories\fft_decomp`, HEAD observed by `git rev-parse HEAD`: `611b504d25d4a9b03fab8023d7dbea75fe381d5d`. Every C path/symbol here is pinned to that revision; line numbers are navigation aids. Managed project baseline for this investigation: `2aa8f0c`.

**Follow-up completed:** [Supplied US v3.0 BIOS verification](bios-rng-verification.md) now resolves the state width, recurrence, overflow and seed-to-output vectors using the user's hashed BIOS and actual instruction execution. ZOD-005 is complete for that pinned BIOS and ZOD-013 is ready. This note preserves the initial source investigation and its limits: the decomp checkout itself still contains only BIOS call veneers. Full-game initial stream position, other BIOS revisions and whole-command draw equivalence remain separate questions.

Evidence classification follows [fixture format v1](fixture-format.md): supported claims below are `source-derived` / `source-traced`. The formula arithmetic and raw-value tapes in this initial note have not been executed against the FFT formula chain. The follow-up separately records `executable-reference` BIOS generator vectors, local capture metadata and emulator limits. Do not extend that generator evidence to the unexecuted formula tapes or original-game behavior.

## What the reference proves and what it omits

- `src/platform/psyq/libapi/rand.c`, `rand` (retail veneer address comment `0x8002230c`), consists of `PSYQ_BIOS_TAIL_CALL(PSYQ_BIOS_TABLE_A, PSYQ_BIOS_A_RAND)`.
- `src/platform/psyq/libapi/srand.c`, `srand` (`0x8002231c`), tail-calls the corresponding seed service.
- `include/psx/bios.h` defines table A as `0xa0`, RAND selector `0x2f`, SRAND selector `0x30`. `include/psx/libc.h` declares `int rand(void)` and `void srand(unsigned int)`. `include/psx/types.h` identifies `s32`/`u32` as signed/unsigned `int`; the target ABI is 32-bit.
- These establish a 32-bit signed return interface and unsigned 32-bit seed interface, **not** internal state width, seed truncation, recurrence coefficients, signed versus unsigned update, overflow semantics, output shift/mask, location, period or serialization format. Source consumers assume a nonnegative 15-bit return (0..32767). That range is explicit in `formula_get_random`'s comment and its scaling constants, but the veneer itself cannot establish the BIOS implementation.
- Searching the reference source for `srand(` finds startup, breeding and the veneer. No local body establishing the PRNG recurrence was found. This is a finding about this snapshot, not a claim that no external BIOS reference exists.

Do not substitute `System.Random`, a host C library `rand`, a remembered LCG, an emulator's HLE implementation, or a guessed 32-bit state and call it PS1-compatible. Even matching some initial outputs would need evidence for state evolution and boundary seeds.

## Seed sites and global stream boundary

`src/main/startup.c` calls `srand(1)` during startup, after `zodiac_load_frame` and before fading out the logo (approximately 88). This is a seed call, not evidence that the first battle begins at the first output from seed 1: later code also draws from the same `rand` service.

`src/world/formation_units.c`, `breeding_update` (340), reseeds with:

`frames * 100 + seconds * 10 + minutes + hours + frames`

The expression is equivalent to `frames * 101 + seconds * 10 + minutes + hours` where signed arithmetic is representable. The play-time fields are `s32` in `include/fft/save.h`. The resulting argument converts to unsigned `int` at the seed interface. Valid play-time domain and signed-overflow behavior for malformed counters have not been established; do not infer modulo arithmetic for the signed expression from the interface. Example representable arguments: all zero -> seed argument 0; frames=1, seconds=2, minutes=3, hours=4 -> argument 128. Neither predicts the first output without BIOS evidence.

Other consumers include `src/battle/effect_system.c`'s direct `rand()` calls for effect motion/spawn choices (for example 3136..3150) and world/unit-generation routines. Thus reproducing a global retail stream can depend on calls outside deterministic combat. The future managed simulation must have explicit input/output RNG state, independent of renderer, clock and I/O. Whether exact retail stream equivalence needs recorded incoming BIOS state or a documented isolation deviation remains unresolved. A fixed deterministic combat seed is useful for synthetic replays but is not automatically the retail incoming state.

## Supported range conversions and widths

`src/main/unit_growth.c`, `random_roll_pass_fail` (844):

`s32 roll = rand() * maximum / 0x8000; return roll >= threshold;`

`maximum` and `threshold` are signed 32-bit values. Under the assumed 15-bit nonnegative raw output and positive, representable product, integer division truncates downward: `floor(raw * maximum / 32768)`. Return 0 means below threshold (the caller's success), return 1 means at least threshold. This is multiplication/division scaling, **not** modulo. The function always calls `rand`, including maximum=0, threshold<=0, and threshold>=maximum. It does not clamp its threshold or skip a certain outcome.

For the selected formula, the evade denominator is a `u8` base hit promoted through a `u16` parameter; thresholds are `u16` evade fields. Critical/proc maximum is 100. Products are safely within signed 32-bit. Do not broaden this arithmetic to arbitrary signed maxima without defining its overflow behavior: for raw 32767, maximum 65538 gives product 2147483646 (still representable); 65539 gives mathematical product 2147516413 (beyond `Int32.MaxValue`). The latter is an **unsupported overflow boundary**, not a source-proven wrapped output. The source C's signed multiplication does not itself specify a portable overflow result; an executing target instruction trace would be needed for a compatibility claim outside the bounded domain.

`src/battle/action_outcomes.c`, `formula_get_random` (397), returns `0x4000` without a BIOS draw whenever `g_action_state != ACTION_STATE_EXECUTE`; only execution calls `rand()`. `formula_roll_random_at_least(limit, threshold)` (405) scales that result with the same multiply/divide and returns the same comparison polarity. This distinction does not apply to every helper: `random_roll_pass_fail` calls the BIOS directly, even when invoked during preview.

`include/fft/random.h`, `RANDOM_BETWEEN(min,max)`, is a different operation: equal bounds return the bound with zero draws; otherwise one draw is reduced by remainder and offset by the smaller bound, excluding the upper bound. It is unused by the selected formula chain. Do not unify its modulo semantics with combat threshold scaling. Restrict any future macro-equivalent tests to safe argument differences and side-effect-free arguments.

## Bounded proposed physical attack

Select one invocation of `formula_weapon_damage` (formula `0x01`), entered for one already selected target while executing. A normal sword with deterministic PA-based XA is proposed; the exact source item/ability content record and full legal target setup remain ZOD-021 work. Do not claim every weapon named sword necessarily dispatches this formula without checking its record.

Preconditions: Attack command, one ordinary non-Frog/nonmonster attacker with one sword and no Two Swords; one unmounted target; no reactions, counters, Mimes, poaching/training or other follow-up actions; positive base hit (100 for the examples); initialized hit=true, accuracy=100; no target nullification/absorption, element effects, random weapon power, randomized status selection, separate-per-status rolls or weapon spell proc. The loaded status record has zero status bits and uses no random/separate mode. Use ordinary positive PA small enough for damage arithmetic, for example PA=5; modifiers are neutral. Front/side facing and actual evade fields are explicit inputs to this formula-level investigation; this is not a full battle fixture or target-legality proof.

In `src/battle/action_resolution.c`, `action_calculate_target_result` (448) selects `weapon->formula` for the Attack menu, validates its formula selector, loads the status entry, invokes `reaction_check_evasion`, exits early on an already missed target, then dispatches `g_formula_handlers[formula - 1]`. Table entry 1 is `formula_weapon_damage`. Reactions can draw before formula entry, so they are explicitly excluded; formula draw counts are measured at the dispatch boundary, not across menus, target selection, presentation or the entire turn.

`src/battle/action_preparation.c`'s strike initialization (around 854) has random-count branches for truth/untruth magic and Two Swords handling. Neither applies to the selected one-sword Attack. This does not prove zero draws in every possible action-preparation/rejection path.

## Exact formula draw order and short circuits

`src/battle/formula_weapons.c`, `formula_weapon_damage` (127), calls physical evade first. Only if it returns 0 does weapon damage run; only if that returns 0 does status application run.

1. `src/battle/formula_effects.c`, `formula_evade_calculate_physical` (879), resolves evade modifiers and facing, then `formula_evade_roll` -> `src/battle/formula_core.c`, `formula_evade_calculate_final_hit_percent` (967). It performs accessory, right-hand, left-hand, class evade checks **in that order** using `formula_evade_roll_source` (447). Each check calls `random_roll_pass_fail(baseHit, evade)`, including evade zero. An executing successful evade sets hit=false and returns early, so all later evades and damage rolls are omitted. There is no extra single aggregate accuracy roll after the four checks.
2. If all four evades fail to evade, `formula_calculate_weapon_damage` (effects, 928) sets XA/YA by weapon type, applies charge and element strengthen, then physical modifiers. `formula_calculate_base_xa` (core, 1029) uses PA directly for a sword, with **no draw**. Axes/hammers/bags draw here; they are excluded.
3. `formula_calculate_critical_hit` (core, 1311) calls `formula_roll_random_at_least(100, 4)` once (`FORMULA_CRITICAL_HIT_CHANCE` in `include/fft/formula.h`). If its scaled roll is <4, it flags critical and calls `formula_get_random` once more for XA bonus `base_xa + raw * base_xa / 32768`. `base_xa` is `s16`; multiplication uses signed 32-bit, then stores back into the formula's XA field. `move_knockback_calculate` follows the critical bonus but has no random call. It can alter geometry/results; this note's draw counts do not promise no knockback.
4. Damage is stored, then weapon element modifiers run. If they clear hit, weapon damage returns before the added-effect roll. In the selected neutral non-nullified case, absorption handling is followed by `formula_roll_conditional_status_proc` -> its inner helper (effects, 413), which calls `random_roll_pass_fail(100,19)` once **even when the loaded status set is empty**. A critical bonus draw shifts the proc roll's position.
5. If the proc roll succeeds, `formula_apply_status` -> `formula_apply_status_to_action` (effects, 656/679). The chosen empty, nonrandom status record adds no draws; status failure restores the prior hit/accuracy/miss type. Random-one and separate status modes can add draws and are excluded. Reactions and post-action processing outside the formula are excluded as well.

Formula-entry draw totals while executing: accessory evade =1, right-hand evade =2, left-hand evade =3, class evade =4. A hit with no critical uses 6: four evades, critical check, proc check. A critical hit uses 7: four evades, critical check, bonus, proc check. A nullification after damage, outside the selected inputs, would omit the final proc check while retaining earlier draws.

Even an evade at/above base hit does not bypass drawing: the routine first marks hit=false/accuracy=0, then proceeds into the ordered roll checks. An executing certain accessory evade still draws once. Zero evades still draw four times. These are compatibility behaviors; optimizing away known outcomes changes the stream.

Preview/AI difference: the evade helper calls direct `random_roll_pass_fail` before checking action state. Outside execution a successful evade returns 0 without clearing the target, so all four evade checks still draw. Critical helpers use the fixed midpoint (scaled 50, no critical, zero draws); the proc inner helper is guarded by execution and does not draw. For the selected zero-evade preview formula, there are four actual draws, not zero. A pure managed preview must either model those original effects explicitly or record the intended deviation/isolation policy; do not accidentally mutate authoritative simulation state from UI preview.

## Source-derived arithmetic and draw-tape vectors

These are direct-helper expectations for **supplied raw return values**, not generator-sequence vectors. No assertion about a seed producing these tapes is made.

- Scaling maximum=100: raw 0 -> roll 0; raw 32767 -> roll 99. Threshold 0 always returns 1 (one draw); threshold 100 always returns 0 (one draw), for the assumed raw range.
- Critical boundary: raw 1310 -> roll 3 -> return 0/critical; raw 1311 -> roll 4 -> return 1/noncritical. Threshold equality fails the chance, rather than succeeding.
- Proc boundary: raw 6225 -> roll 18 -> proc succeeds; raw 6226 -> roll 19 -> proc fails.
- Evade boundary, maximum=100, threshold=10: raw 3276 -> roll 9 -> evade; raw 3277 -> roll 10 -> no evade.
- Maximum=256: raw 0 ->0; raw 32767 ->255. Maximum=65535, raw 32767 ->65533 (product 2147385345). Maximum=65538, raw 32767 ->65535 (product 2147483646). These last values demonstrate safe signed-product boundaries, not the selected sword's parameters; maximum 65539's overflow result is unresolved.
- `RANDOM_BETWEEN(5,5)` ->5 with zero draws; `(3,8)` and `(8,3)` with raw 32767 ->5 (`32767 % 5 + 3`). Same raw scaled to width 5 instead gives 4 before offset; these are different conversions.
- Execute, four evade thresholds zero, raw tape `[0,0,0,0,1311,6226]`: all four evades draw and fail to evade, critical false, proc false; consumes 6. No unused replacement draws are generated.
- Same preconditions, tape `[0,0,0,0,1310,32767,6225]`, precritical XA=5: critical true, bonus=4, XA becomes 9, proc true; consumes 7. No random status mode is selected, so the empty status set adds none.
- Execute, accessory evade 10, other evades zero, tape `[3276,...]`: miss at accessory, consumes 1; no critical/bonus/proc. With `[3277,0,0,0,1311,6226]`, accessory fails to evade, all remaining zero evades still draw, then critical/proc fail; consumes 6.
- Execute, accessory 0/right 10/left 0/class 0, tape `[0,3276,...]`: right evade miss, consumes 2. Moving the sole evade 10 to left or class and placing 3276 there yields respectively 3 or 4 draws. Later tape entries are untouched.
- Execute, accessory=100/baseHit=100, raw tape `[32767,...]`: initial certainty check marks miss, the accessory still draws, roll 99 <100 evades, consumes 1.
- Preview, zero evades and any four raw values in 0..32767: consumes four direct evade draws; subsequent critical uses fixed `0x4000`, and proc uses no draw. This is not evidence that every preview routine in the game uses precisely four draws.

The initial missing overflow/seed vectors are now supplied in the [BIOS follow-up](bios-rng-verification.md): boundary seeds, consecutive states/outputs, multiply/add wrap boundaries and reseeding. These are separate generator vectors observed by executing ROM instructions, not results obtained by executing this note's formula-level raw-value tapes.

## Failed actions versus rejected commands

A legal attack that evades is a resolved miss and **does consume** the short prefix above. An empty status set does not remove the 19% added-effect roll. Failure to apply a status does not rewind earlier draws.

`formula_force_attack_miss` (core, 212) only clears hit/miss/accuracy and makes no direct random call. That proves the helper itself consumes zero, not that every caller reaches it before any draw. Likewise `action_calculate_target_result`'s early return after a reaction miss does not establish zero earlier draws: the reaction may have drawn.

There is not yet a source-traced whole-command contract for invalid target, out-of-range target, dead target, cancelled menus or already-used action. ZOD-021 must trace legality and its placement before these helpers. A future managed command rejected before formula dispatch can preserve RNG state as an explicit synthetic API contract, but must not label that universal zero-draw rule original-game behavior without the caller evidence. Full hit/miss/damage/KO/turn consequences and follow-up reaction draw ordering remain outside this spike.

## Neutral engine seam and FFT ownership

The neutral engine can support explicit immutable RNG state threaded through transitions, stable algorithm identity/version, reproducible serialization, and a testable ordered draw trace/count. No ambient generator, wall clock, renderer call or platform BIOS belongs in deterministic engine execution. Preserve the complete state required by the selected algorithm once known; do not assume the 32-bit seed is the entire state.

FFT owns the BIOS-compatible algorithm choice, eventual recurrence/output arithmetic, seed/import policy, 15-bit output semantics, multiply/divide versus modulo conversions, threshold polarity, critical/proc probabilities, and formula-specific draw order/short circuits. An unrelated game may choose another generator. A neutral seam does not require moving these helpers/constants into Zodiac.

The supplied BIOS follow-up establishes one full 32-bit word as the generator state. ZOD-013 can now use that evidence for the FFT-owned generator while keeping the engine seam neutral. Fixture v1 currently has null RNG state and no attack events; an explicit schema extension is still needed before replay can compare real states and draw traces.

## Concrete follow-up evidence plan

1. Identify an edition-appropriate BIOS implementation or verified original BIOS routine for selectors A0:2f/30. Record BIOS identity/hash and exact routine evidence; do not copy BIOS/media into Git. If using an emulator's HLE source, pin its revision and classify it as an independent implementation hypothesis until compared with actual BIOS behavior.
2. Trace its seed/state storage, update instructions and return extraction. Record precise widths, modular/signed behavior, and whether callers beyond this executable can alter state. This is the missing prerequisite for generator overflow vectors and reproducible state serialization.
3. Run an isolated harness against that actual implementation (or emulator BIOS execution) using the boundary seeds listed above and an overflow-triggering state; record outputs and before/after states. Compare with the independently implemented managed generator. A transcription of a guessed LCG running in C is not independent validation. A host C runtime's `rand` does not execute these tail veneers' BIOS body.
4. Separately invoke the pinned formula chain with initialized scratch/units/content and instrument every raw draw. Use the supplied tape vectors to check ordering, then a known incoming BIOS state to check full outputs. Record harness revision/compiler/options, input construction and return data. Original/emulator attack observations require verified edition/assets and controlled initial RNG state.
5. Keep oracle binaries, emulator saves and outputs under ignored `scratch/behavior/<fixture-id>/`; commit metadata with hashes and procedures only. No oracle/C toolchain/PS1 runtime dependency enters the application. Runtime continues to use prepared files, never the BIN.

The [BIOS evidence and state/overflow vectors](bios-rng-verification.md) have now been added and reviewed, closing ZOD-005 for the pinned US v3.0 image. Production generator implementation remains ZOD-013; full-command draw equivalence and the source trace for action legality remain later work. The plan above is retained as the original investigation's reproducible evidence requirements.
