# Living magic result projections

## ZOD-370

Source study is pinned to clean read-only `fft_decomp`
`11464943bbdbd2a0af022d90cb7f10455cf911df`, from managed base
`518a074a4ccd039e1f15c8b8f67b68fb1d95fcb8`. The
[source receipt](../evidence/combat/magic-publication-source-v1.json) fingerprints
10 files/18 functions. The [36 descriptions](../fixtures/behavior/magic-publication-contract-v1.json)
separate source arithmetic from explicitly bounded managed projection admission.
They do not execute original/C/managed code or claim full action application.

### Source ordering and storage

`action_init_current_data` calls `mp_check_and_consume` before strike initialization
and per-target calculation. Execution/AI pay once; Preview skips the MP store.
Source costs are byte records, while the managed cost API deliberately uses int.
The stored source MP field is unsigned16 but affordability reads it signed16.
Neither charge planning nor each target pays again.

`action_apply_strike_results` applies the stored target list in order. Each target
is resolved through the current runtime lookup, then `action_apply_target_result`.
Actor result application is separate and later, via `unit_result_apply` and
`action_apply_attacker_result`; it copies the prepared actor work into the unit's
action record. These are multiple mutations, not a source-wide atomic transaction.

Target application validates slot/activity, handles dead-to-crystal/treasure,
marks primary targeted outcome and dispatches targeted reactions **before** testing
hit. A miss returns before Golem/sharing/HP/MP/status work, but does not prove that
no reaction work or RNG occurred. A Golem guard redirects HP work. Sharing checks
MP Switch, Distribute then Damage Split, and may alter work or queue reactions.
They must be explicitly absent before the bounded vital branch is used.

Old HP/MP and maxima are unsigned16. Damage/healing work is signed16. The source
computes each full signed32 expression `old - damage + healing`, clamps below0,
then above the maximum and stores unsigned16. It does not clamp each component
separately or use the attack-type bits to gate the arithmetic. A hit with zero
work can still clamp an over-maximum stored value. A miss skips those clamps.
`ChangedVitals` describes only these fields, not the source's complete outcome.

Stat/equipment/gil/EXP/JP/level/poach/transformation work follows. HP0 then enters
Dead/KO/status handling. Otherwise Critical is recalculated at `HP <= MaxHP/5`,
Chicken at `Brave < 10`. The HP-damage **type bit** schedules removal of Charm,
Sleep, Confusion and Transparent even if numeric damage is zero or negative.
Knockback/charge cancellation and general status resolution follow. Later reaction
dispatch and attacker EXP bookkeeping are still observable work. The bounded
projection must reject changes that need those deferred systems.

### Modes, scope and selected children

The original AI calls the applicator while simulating and saving/restoring unit
state; that is separate from execution's presentation/controller path. Preview's
`action_calculate_projected_effect` calculates a result under Preview mode and
restores command/context without calling this applicator. Candidate projection
helpers must not grant Preview or AI results execution/publication authority.

ZOD-371 implements exact pure vital arithmetic. ZOD-372 admits a living HP-only
frame with known absent reactions, Golem/sharing and other result work, ordinary
stable Critical/Chicken, and no required damage-type status cancellations.
ZOD-373 adds a validated immutable projection snapshot. ZOD-374 applies a supplied
cast privately, including one proposed MP deduction before self-target work.
ZOD-375 composes a ready plan into an opaque HP/MP/RNG projection proposal.

These are projections of selected vital fields, not the complete source action.
Ability-outcome markers, caster rewards/progression, status work, KO, actor-side
results, game battle/turn state and actual adoption remain separate. In particular,
MP-paid fizzles/empty-target results cannot be silently refunded by treating an
unsupported query rejection as a real executed outcome. Whole-query rejection
and private rollback are managed admission policy until a full command path is
traced. No live publisher, command, global clock or magic AI activation is added.

Owner drafts, JobStats, host/editor, automatic facing, full corpse/Golem/terminal/
status/frame RNG, broad interfaces and ZOD-106 remain deferred. The future custom
forward1/3/5 cone remains unimplemented. The reference checkout is never modified
or used at runtime; prepared original data stays under ignored scratch.

## ZOD-371

`MagicVitalArithmetic` implements the18 raw vital descriptions using unsigned16
stored values, signed16 work and full signed32 sums before clamps. A miss returns
unchanged fields even when above maximum. This is numeric work after earlier
application branches; it does not dispatch reactions, infer type bits, change
statuses or report full source ability outcome. Two focused managed cases pass
all18 literals, immutable replay and null-input handling.

## ZOD-372

`MagicLivingApplication` projects HP-only frames after active-unit and absent
pre-hit reaction facts. A miss preserves input fields before reading downstream
Golem/sharing/status certificates. Hits require absent Golem/sharing and other
record work, ordinary consistent Critical/Chicken and stable status results.
HP0, critical threshold crossings and required damage-type cancellations reject
without an outcome. Numeric HP changes are not gated by type bits; cancellation
checks are. Signed negative work is retained, while positive unfinalized amounts
above999 reject. Derived XP/outcome markers are outside this vital projection.
Five cumulative managed cases pass all36 raw/living descriptions plus selective
knowledge, zero-damage cancellation and immutable-input boundaries.
