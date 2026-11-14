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

## ZOD-373

`MagicProjectionSnapshot` captures an immutable map, source-order roster, current
formula inputs, vital facts and RNG checkpoint. Active units require coherent
living current/vital captures and unblocked in-map positions. Stable IDs/orders
must be unique; current identities and HP/MP bounds must agree. Inactive anchors
can retain explicit metadata with no invented current/vital defaults. This adds
no original fixed roster cap, neutral-sign requirement, sword-state fields or
adoption authority. Eight cumulative managed cases pass copy isolation, inactive
anchors, invalid coherence and a26-unit/13-sign roster.

## ZOD-374

`MagicCastProjection` privately reduces a certified eligible target order into a
new vital snapshot. It checks unique known active targets and a consistent
nonnegative recorded cost/MP difference, proposes MP once before self-target
application and delegates each frame to `MagicLivingApplication`. Active-unit
knowledge is derived from captured metadata; absent reactions/other work still
require explicit certificates. Late rejection discards the entire HP/MP builder.
Input unit order and bystanders remain, and this stage deliberately retains the
input RNG because it does not own formula draws. Twelve cumulative managed cases
pass two-target66/32 HP, one-cost21 MP, self/miss handling, malformed casts and
late KO/Critical rollback. A supplied public cast is analysis data, not authority.

## ZOD-375

`MagicProjectionQuery.Calculate` binds an Execute-mode ready request to an exact
immutable source snapshot. It checks map identity and current roster equality,
recomputes through `MagicReadyPlanQuery` from the captured RNG, derives eligible
source order and then delegates vital admission to `MagicCastProjection`.
Success returns an opaque `MagicProjectionProposal` retaining its source,
candidate and cast. The candidate contains all proposed target HP, one MP cost
and the complete formula RNG continuation. Callers cannot supply calculated
frames, candidate MP or a replacement RNG to this entry.

Preview and AI cannot create these execution projections. Unknown application
facts, foreign inputs, resource/calculation failures and late KO/status rejection
return no proposal and the entire incoming RNG checkpoint. This is private
managed admission, not a claim that original target application is atomic.
Repeated queries preserve their source and produce equal candidate values.
Twenty cumulative focused managed cases pass, including two-target 66/32 HP,
caster 21 MP, RNG 1579910103/count19, late KO/Critical rollback, absorption/healing,
resource failure, counter exhaustion and snapshot substitution rejection.

The proposal conveys no command, turn, one-use receipt or live adoption authority.
[ZOD-376–381](action-outcomes-and-rewards.md) now trace those boundaries, actor/
reward work and paid fizzles, and implement bounded reward calculations.
[ZOD-382–384](growth-and-job-unlocks.md) now supply raw growth/RNG and job unlock
calculations; derived refresh and complete career/battle-state integration remain
separate. All earlier deferred systems remain separate.

## Final validation

Locked restore and Release build pass with zero warnings/errors. All **4,714 .NET
cases** and **711 Python tests** pass, including 20 new managed cases and eight new
Python cases. Fresh pinned source, prior prepared-data, algorithm-guide, coverage,
local-link, public-type layout and preservation audits pass. The
[implementation receipt](../evidence/combat/magic-publication-implementation-v1.json)
records milestone bases and ignored reports. The 18 new public types each have a
matching file under Magic/Publication with the existing Framework namespace.
All pre-batch production, managed tests and behavior fixtures, owner drafts and
JobStats are unchanged. No Content, AI, host, project/dependency or import changes;
no original/C execution or UI review.
