# Ordinary action outcomes and rewards

## ZOD-376

Study starts from managed `c4994e4bcb908290fff5cf3090d5fd1ef83879f5` and clean,
read-only `fft_decomp` `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/combat/action-rewards-source-v1.json) pins callers,
storage, arithmetic and downstream consumers. No original or C code is executed.

### Execution and authority

Original action authority comes from the controller's current source/casting unit,
stored command, action phase and global work, not from an externally calculated
result. `action_announce_next` visits Hamedo, actor and reaction phases. Actor
initialization checks/spends MP before resetting reaction/outcome records and
initializing strikes. A failed initialization can already have changed action
statuses; it stores actor data and ends the command. It is not a universal refund.

`action_resolve_strike` can fail panel construction after MP was spent, or return
an empty strike after enabling rewards. Empty strikes advance strike counters and
retain animation/continuation work. They are distinct from a managed admission
failure. Target application is sequential. Caster-result application happens later
in battle-message display, by copying separate actor work through the same
applicator. After continued strikes/reactions, `reward_grant` restores facing,
applies rewards and enters level/job-level/learn-on-hit reports. Command completion
is later still. A charge-ready event is not necessarily the actor's ordinary turn.

The current sword session and turn snapshot cannot represent this whole contract:
magic current captures have MP but no career/raw growth/unlocks, while the vital
projection intentionally excludes actor work and complete statuses. ZOD-375's
opaque proposal is bound calculation data, not a one-use execution receipt.
Do not adopt it into sword state or consume an ACT/facing receipt. A future command
must own coherent battle/career/status state, pending-plan identity, phase/turn
bookkeeping and fresh recomputation, with paid failures represented explicitly.
Managed atomic admission is an architecture choice, not original mutation order.

### Outcome markers and EXP

Successful primary initialization clears every slot's outcome. In normal target
application, primary context assigns TARGETED (1) before reactions and the miss
test. A miss retains that assignment. On the normal returning hit branch, any
actual change assigns AFFECTED (2), even in nonprimary context; these are stores,
not bitwise ORs. A later actor application can overwrite its earlier self-target
marker. The HP-zero early-return branch awards kill EXP before returning, without
the later AFFECTED assignment. Dead-to-crystal/treasure has an earlier bypass.
Those two branches are excluded from the ordinary marker helper. Learn-on-hit
tests the AFFECTED bit and replaces the marker with LEARN_CHECKED (4).

`attacker_exp_update` takes the maximum award, never sums targets. Primary context
and nonzero outcome are its actual guards: the comment's simulation claim is not
an additional mode test in this function. Changed outcome starts at10; killed
starts at20 divided by the target's current byte KO count, with zero using20.
The KO count was already incremented on executing death paths. Add target level
minus actor level, floor at1, double for Gained EXP Up, cap at99, then compare to
the stored byte accumulator. Levels and KO count are byte inputs; signed16
intermediates do not overflow for this domain. No RNG is used.

### Reward grant and JP

`reward_apply_action` clears display first. Hamedo rejects before checking the
can-earn flag; a clear flag returns success without even consulting the actor.
Otherwise absent, Crystal, Dead, Petrify or Treasure actors reject. Add earned
EXP to stored byte EXP and cap at255 before `unit_level_up_check`. At100 or more,
one level is gained and all excess EXP is discarded; at level99 or higher EXP
becomes99. Growth and derived-stat rebuilding are separate required operations.
The JP formula uses the level captured **before** this level-up.

Monsters receive EXP but stop before JP. Other actors use
`8 + 2*oldJobLevel + (oldLevel >> 2)`, or zero if earned EXP was zero.
Every other slot is offered integer JP/4 before Gained JP Up; actor JP is then
multiplied by3 and divided by2. Recipient initial team must match actor current
team, while the actor's own award uses its own initial team. Charm can therefore
change spillover recipients without preventing the direct award.

`reward_add_job_jp` rejects negative amounts, absent/Crystal/Dead/Treasure,
monsters, other initial teams, female Bard or male Dancer. **Petrify alone does
not exclude a spillover recipient.** Male and female bits remain independent.
Zero JP still stores clamped current/total fields, recalculates job level and
rebuilds unlocks. Current and total unsigned16 JP clamp independently at9999;
job level comes from total. `job_level_calculate` counts each satisfied threshold
1..8; it does not stop at the first unmet threshold. Packed job levels, unlock
requirements and sex-dependent unlock masks belong to prepared content/adapters.
Original job75..93 maps to slots1..19; all other byte job IDs use slot0.

The prepared SCUS_942.21 hash is checked before auditing the threshold table at
0x80066182: levels1..8 require100,200,350,550,800,1150,1550,2100 JP.
Only that small behavioral table is recorded; the original file stays in scratch.
The arithmetic helper accepts explicit immutable threshold data without loading
files or imposing an original roster/job table size on future content.

### Selected children and limits

- ZOD-377: ordinary target outcome-marker transitions, including misses/reactions.
- ZOD-378: exact per-target EXP maximum accumulation and kill-count boundaries.
- ZOD-379: reward EXP-balance and pre-level JP/direct/spillover calculations.
- ZOD-380: current/total JP saturation, threshold counting and original slot mapping.
- ZOD-381: compose ordinary reward offers over a stable-ID roster, with source
  actor/recipient eligibility, independent sex bits and immutable results.

These implement missing reward calculations, not full career or battle adoption.
Results must explicitly report required growth/unlock recomputation. New job
levels cannot silently become a complete career state. Full status/corpse/Golem/
terminal/frame RNG, host/editor, automatic facing, global scheduling, magic AI,
owner drafts/JobStats, broad interfaces, ZOD-106 and the custom cone stay separate.
Next trace growth and unlock requirements before selecting live integration.

## ZOD-377

`ActionOutcomeTracking.ApplyOrdinary` implements normal-return marker stores.
Primary misses assign Targeted; a nonprimary changed hit assigns Affected;
otherwise nonprimary calls preserve the prior byte, including unknown bits.
There is no cumulative OR. Three managed cases cover eight independent literals,
reaction preservation and a later actor miss replacing its self-target marker.
Early KO/dead-conversion and learn-on-hit application remain separate.

## ZOD-378

`ActionExperience.Accumulate` owns primary-context maximum EXP work. It accepts
byte levels and the already-updated KO count, floors before doubling, caps the
new award at99 and preserves a larger existing accumulator. It does not add a
mode guard absent from the source. Three managed cases pass14 literals, a
multi-target/reaction tape and post-increment KO255/0 boundaries. The helper
does not inflict KO, increment its count, apply career EXP or draw RNG.

## ZOD-379

`ActionRewardCalculation` computes pre-level JP amounts and numeric EXP/level
balances. Monsters bypass job-level reads; other actors retain the source nibble
domain. EXP overflow is capped before one level check, excess is lost and level99
holds EXP99. A numeric level change explicitly requires stat growth, with no
claim to updated raw/derived stats. Four further managed cases pass16 literals,
pre-level3 versus new-level4 JP and the monster early return. Ten cumulative
managed cases pass; no battle or career state is published.

## ZOD-380

`JobPointArithmetic.Add` caps current/total independently at9999 and derives job
level from total using explicit immutable thresholds. Zero awards still clamp
and count; counting does not assume sorted or distinct thresholds. The original
eight prepared thresholds pass all immediate lower/exact boundaries. The helper
does not pack levels or rebuild unlocks and rejects negative or potentially
signed32-overflowing awards rather than claiming unsupported overflow behavior.
`OriginalRewardSlot` confines byte job-ID mapping to the compatibility adapter.
Four more managed cases pass11 literals, threshold boundaries, job endpoints and
domain guards;14 cumulative managed cases pass.

## ZOD-381

`BattleActionRewards.Project` composes actor eligibility, EXP balance, pre-level
JP amounts and eligible recipient job-slot projections. Captures use stable IDs,
unique source order and classified teams; JP facts refer to the actor's selected
job slot. Hamedo and disabled-reward gates precede actor reads. Other recipients
are visited in source order and actor storage is last. Petrify only blocks the
actor; independent sex bits filter Bard/Dancer storage without normalizing flags.
An incompatible actor sex can prevent its own JP storage while its displayed
earned amount remains nonzero, as in the source. Zero awards still enter storage
arithmetic and require unlock recomputation. Monsters bypass all JP captures.

The result explicitly reports `RequiresStatGrowth` and
`RequiresUnlockRecalculation`. It projects selected numeric fields, not a complete
career, grants/unlocks, raw stats or a live command receipt. Invalid or mismatched
captures expose no partial result. No original roster cap is imposed. Replays
preserve inputs. A managed scenario composes the prior two-target magic proposal,
actual changed markers and maximum EXP14 into actor JP18/spillover3 calculations,
without publishing any field. Eight further independent roster descriptions
include current-team charm, petrified recipients, zero awards, incompatible actor
sex, monster and level99 paths. Full original action/career adoption still waits
for complete derived refresh and the remaining phase/status/fizzle work.
[ZOD-382–384](growth-and-job-unlocks.md) now trace growth/unlocks and implement
raw growth/RNG and semantic unlock calculations. ZOD-385 adds the bounded mode1
numeric derived-attribute projection next.

## Final validation

Locked restore and Release build pass with zero warnings/errors. All **4,748 .NET
cases** and **720 Python tests** pass, including 34 new managed cases and nine new
Python cases. The 57 independent descriptions include eight complete roster
projections. Fresh source fingerprints (11 files/23 functions), eight verified
prepared thresholds, prior magic/prepared-data, algorithm-guide, coverage,
local-link, layout and preservation checks pass. The
[implementation receipt](../evidence/combat/action-rewards-implementation-v1.json)
records milestone bases and ignored reports under `scratch/action-rewards-376-381/`.
All 15 new public types have matching files in Battle/Combat/Rewards, retaining
the Framework namespace. All pre-batch production, managed tests and behavior
fixtures, owner drafts and JobStats are unchanged. No project/dependency, Content,
AI, host or import changes; no original/C execution or UI review.
