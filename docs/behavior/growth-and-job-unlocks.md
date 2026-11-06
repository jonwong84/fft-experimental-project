# Battle growth and job unlocks

## ZOD-382

Source study starts from managed `24c1779e6cb7fb2aee0b39530b93942eef3baada`
and clean read-only `fft_decomp` `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/combat/growth-unlocks-source-v1.json) fingerprints
the callers, raw arithmetic, refresh chain and unlock consumers. The independent
[descriptions](../fixtures/behavior/growth-unlocks-contract-v1.json) separate
original content from explicit synthetic prerequisite cases. Neither verifier
executes original/C/managed code; prepared data stays in ignored scratch.

### Growth callers, widths and RNG

`reward_apply_action` adds/caps EXP, then `unit_level_up_check` calls
`unit_level_growth_apply(unit, 0)` **before** incrementing the level. Below100 EXP
no growth runs; level99 or higher holds EXP99 without growth. Excess EXP is lost.
In contrast, `level_change_apply` stores the new capped level first, then grows
or shrinks in Execute mode only. At a level boundary those special effects can
still run growth even when the capped level is unchanged. Its effect-presence
outcome is not the same as actual mutation in nonexecution modes.

`unit_level_growth_apply` processes HP, MP, Speed, PA, MA in that order. Each raw
value is unsigned24, stored low/middle/high bytes. Growth is a byte; zero becomes1.
The divisor is `max(growth,1) + currentLevel`; integer quotient is added for up,
subtracted for down, then capped at0xffffff. For byte levels/growth, the divisor
is1..510 and addition fits unsigned32. The raw half-step does not change level.
It calls `rand()` **once per stat**, discarding all five outputs, including zero
quotients, zero values, level-down and saturated growth. The existing verified
BIOS RNG is the managed stream owner. Diagnostic counter exhaustion is a managed
admission boundary: reject the whole private calculation with input RNG intact;
the C routine itself mutates/draws sequentially and has no diagnostic counter.

`party_levels_gain` is different: it iterates roster levels without these five
draws. Generated-unit initialization also replays growth numerically without
calling the battle growth routine. Those paths must not be substituted for
battle growth or silently activated by this work.

### Derived refresh is required, and has observable quirks

After the raw loop, growth calls `unit_stats_refresh`, then clamps current HP/MP
to refreshed maxima. Refresh saves inflicted/current statuses, reloads job
movement, equipment categories, status/affinity records, rebuilds R/S/M and
equipment, reconciles innate/inflicted statuses and timers, recalculates Critical
and queues changed status display flags. `unit_move_jump_calculate(...,1)` keeps
current HP/MP; growth does not refill them.

The growth/multiplier pairs used by the raw routine are the unit's copied
`stat_growths`; `job_data_copy` originally loads those from job content.
`stats_refresh` does not copy that table again. Its equipment-attribute call uses
argument0, which calls `initial_stats_calculate` in mode1. That mode recomputes HP/
MP maxima but **keeps base Speed/PA/MA**. Do not replace the refresh with a generic
all-stat recomputation or silently repair this source behavior. Equipment totals,
status notifications and derived-state adoption remain outside the raw helper.
The selected Squire growth bytes11/15/100/60/50 and multipliers100/75/100/90/80
are checked against verified prepared job74 at table0x800610b8, stride48.

### Unlock rules, original packing and recomputation

`reward_add_job_jp` stores current/total JP, calculates the selected slot's job
level from total, replaces just its nibble and calls `job_unlocks_calculate` even
for zero JP. The unlock routine copies all ten packed bytes, treats a female's
Bard prerequisite level as15 and a male's Dancer prerequisite level as15, then
returns no jobs for monsters. Male and female bits are independent. Nibbles may
be0..15; they must not be normalized to the JP-derived cap8.

Squire is always unlocked for a nonmonster. Each other job unlocks only when all
twenty level requirements are met, comparing the high and low nibbles separately.
Finally male masks out Dancer and female masks out Bard. With neither bit, neither
exclusion applies; with both, both prerequisite substitutions and exclusions
apply. Output is recomputed from levels, not ORed into the previous unlocked set.
`job_unlocks_store` serializes the low24 bits most-significant byte first; original
twenty jobs occupy bits23..4. That packing belongs to adapters, not semantic rules.

The19x10 prerequisite bytes at0x800660c4 are checked from the hash-verified
prepared SCUS_942.21 and normalized to stable job IDs/required levels in the
fixture. Retail requirements do not depend on Bard/Dancer levels; explicit
synthetic rows exercise the source's otherwise latent opposite-sex substitution.
No original catalog or runtime importer is activated. The semantic catalog can
contain more than twenty jobs, without copying the original bitset capacity.

### Selected implementation and remaining gates

- ZOD-383: implement the complete five-stat raw-growth/RNG calculation, including
  level-down, discarded draws, saturation, replay and counter rejection. Return
  raw values only; derived/status refresh and level ownership remain explicit.
- ZOD-384: implement a validated immutable stable-ID prerequisite catalog and
  source unlock evaluation, preserving independent sex flags and nibble levels.

ZOD-385 will select the derived refresh boundary before complete magic/career
state integration. These calculations cannot clear the previous reward result's
growth/unlock requirements by themselves. No live battle/career/turn publication,
paid-fizzle execution, grants/learning, full statuses/reactions, global scheduling,
magic AI, host/editor, automatic facing or custom cone. Owner drafts/JobStats,
broad interfaces and ZOD-106 remain preserved/separate.

## ZOD-383

`BattleRawStatGrowth.Calculate` implements all five raw values and discarded draws
from explicit copied growth rates and caller-established level. It validates
unsigned24 inputs, retains zero-rate fallback, level-down and positive saturation,
and returns a full five-draw continuation. Counter exhaustion returns no raw
prefix and preserves incoming RNG. Five managed cases pass15 independent
literals, exact counter end, zero-valued up/down tapes, immutable replay and a
reward old-level versus special new-level comparison. No derived stats, statuses,
current HP/MP or caller level are changed by this raw calculation.
