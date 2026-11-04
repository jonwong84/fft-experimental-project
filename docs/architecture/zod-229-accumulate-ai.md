# Accumulate AI usefulness, projection and scoring — ZOD-229

Source study, 2026-11-04. Managed base: `6212323` (full revision in the
[evidence manifest](../evidence/ai/accumulate-contract-v1.json)). Read-only
`C:/repositories/fft_decomp` HEAD was rechecked clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. All source symbols below refer to
that revision; the manifest fingerprints 12 files and 41 function bodies.
No original or C program was executed. This study changes no managed runtime.

## Retail data selects a special route

Accumulate is ability 146 (`0x92`), Basic Skill 5 slot 0, formula `0x36`, Y=1.
The previously extracted executable `scratch/all-data-extraction/SCUS_942.21`
is 356,352 bytes, SHA-256
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
The new audit rechecks that identity and the selected primary/secondary fields.
The primary eight-byte record starts at file offset `0x4f880`, the secondary
14-byte record at `0x50bec`; address/table mapping is in the prior action study.
The prepared input and raw records remain ignored; the BIN is not read.

The primary AI metadata says **Target Allies, Affects Stats, Target Map Tiles,
Usable By AI**. It does not request Random Use, Follow Target, evasion, reflection,
HP/MP effects, status changes, or physical/magical attack classification. The
secondary record has range/area/vertical 0, Auto, No Follow Target, zero element,
CT and MP cost, and an empty status-infliction record. The prior
[actual-action study](zod-104-accumulate-action.md) owns those targeting/formula
details. AI Target Map Tiles is a different field from the secondary Auto flag.

The synthetic Accumulate sample has zero AI metadata bytes. Its executable
effect binding proves PA+1, not retail AI eligibility. Do not infer these AI
facts from the name, effect binding, or synthetic learning metadata.

## List initialization and usage before search

`abilities.c::usable_abilities_build` appends Attack, primary-skillset actions,
secondary-skillset actions for humans, and Frog Attack, in that order.
`ability_usable_from_skillset_add` traverses action slots in source order and
requires static AI usability, affordable maximum MP, applicable weapon gates
and `ai_ability_is_known`. Basic Skill's first slot supplies Accumulate when
learned; job membership alone is insufficient. The original known-bit mapping
and its unusual handling of other skillsets remain source behavior, not a
replacement for explicit managed grants. Packed IDs combine a ten-bit ability
with the physical unit slot in the upper bits.

`ai_unit_abilities_init` loads every listed entry, including entries later made
unusable, for ability-count and evasion/MP/Silence quarter-share statistics.
Accumulate increases the denominator without increasing those numerators.
The low three usage bits are recomputed, preserving the upper five. Accumulate
has no trajectory or exhaustive-origin mask, no Cannot Hit Caster/Can Target
Enemies secondary bit, and AoE 0, so **Force Consume Evaluated Aim Tiles** is
set. Preserve retained upper policy bits; do not replace them with the static
fourth AI byte. `turn.c::ai_ability_entry_load` copies primary flags but replaces
working flags4 with this entry's usage byte, then loads secondary data.

`ability_considerations_set` first refreshes usability for all in-play units:
Frog, Don't Act, Silence/Faith timing, MP and inventory rules gate their entries.
In the ordinary living, non-Frog, no-restrictions, zero-MP profile Accumulate
passes. It counts as helpful, without being healing, ranged, or enemy-harmful.
Its ally minimum range is zero; the eventual ally reach is Move. It does not
add an enemy minimum range. Other listed entries still determine those totals,
Mostly Support, evasion shares and random-use threshold; reuse source counting
rather than adding this action to an unchanged Attack/Stone certificate.

The actor-only second pass sets Consume Evaluated Aim Tiles from Force Consume.
This bypasses its optional self-effect classifier, so **preparation adds no
Accumulate usefulness draw** in this route. Usable is cleared after these
summaries when Act was already used. Stat-changing abilities are not disabled
by the status-only buff pruning. Zero CT remains zero through the documented
AI support-bit CT quirk; this does not resolve that broader quirk.

## Caller order and the usefulness coin

`general_select` builds movement scenarios, seeds the move baseline, evaluates
keeping an existing charge, then selects active targets and evaluates the
ability list in order before the ranked lottery. Other policies can enter
different masks/passes first. The bounded no-special-policy pair must preserve
that preparation/search order and the already-admitted Blue/NonBlue distinction.

`ability_list_evaluate` loads each entry and calls
`evaluation.c::ai_ability_outcome_evaluate`. Fresh evaluation checks usable,
then Random Use if requested (Accumulate does **not** request it), writes the
action's skillset/ability/item, and copies the active mask to the targetable mask.
For retail Accumulate the **map-target branch** then:

1. Sets unit targeting with **target ID = actor**, candidate destination from
   the current movement scenario, and classifies any destination pickup.
2. Calls `useful_targets_filter`, which visits active source slots ascending.
   Each calls `ai_target_classify_ability_effect(slot, check_reflect=1)`.
3. If no useful target remains, returns without simulating or inserting this
   ability. Otherwise calls `ai_move_origins_evaluate`, inserts the best rank,
   and returns. It never reaches `origin_self_target_evaluate`, reflected
   alternatives, ordinary unit/tile scans, or the later `ability_effect_on_self`
   classifier in the non-map branch.

The classifier applies Only restrictions, Cannot Hit Caster, timed Reflect/
Innocent/Jump, death/revival/Petrify/Wall and element restrictions first.
Target Allies rejects an ordinary enemy as **Adverse** before any stat coin.
Death Sentence on an allied target with no Add Reraise returns None first.
For an otherwise eligible ordinary ally, the Affects Stats branch tests
`rand() & 1`: **even → Useful; odd → fall through**, then None because retail
Accumulate has no Unequip bit. It reads neither base nor effective PA; PA 99
does not suppress the coin or successful usefulness.

The filter retains only Useful, clearing both Adverse and None entries. In the
closed pair with an active eligible actor and ordinary enemy, this is **one
coin for the actor per fresh ability-outcome evaluation**, irrespective of the
two origin orders. An excluded/blocked actor need not draw. General rosters
can draw for several active eligible allies, even outside this action's real
range, because filtering precedes origin evaluation. Another ally's usefulness
does not certify a valuable self hit. Separate policy/search passes can call
the classifier again. There is no once-per-turn cached coin. A resume into
the map-evaluation stage skips the fresh filter, preserving the previous mask.

## Hypothetical PA, hit accounting and clocks

`ai_move_origins_evaluate` evaluates **ACT then Move** from the current origin
first if permitted. If the candidate destination differs and permits use, it
then evaluates **Move then ACT**. An unchanged destination skips that second
candidate. Each `ai_action_evaluate` owns a fresh
`ai_action_simulate_and_score` baseline; the second candidate must not inherit
the first candidate's PA increase.

The simulation applies action/movement in that order through normal preparation,
`action_hits_simulate` and `strikes_simulate`. Under the dry, flat, distinct-unit,
single-layer, zero-equipment-bonus, no-reaction/mimic/charge/pickup certificate,
the auto, area-zero effect has one target: its actor. The immediate action is
prepared, single-strike, zero cost. `action_clear_current_data` initializes hit
1 and signed-16 accuracy 100. Formula `formula_add_pa_y` writes byte PA change
`1 | 0x80` and pseudo-status attack type. The formula does not roll accuracy,
evasion, critical or a proc. Actual and hypothetical resolution draw zero RNG
within this certificate; turn/upkeep draws remain separate.

`resolution.c::stat_delta_apply` masks the low seven-bit amount, computes in
signed-32 arithmetic from unsigned-byte base PA, clamps to 1..99 before byte
storage, and returns early if base PA is unchanged. On change it rebuilds
effective PA from the new base plus equipment bonus and caps at 99. Base,
equipment and effective arrays are distinct. Examples: 5→6, 98→99, 99→99
with zero bonus; base98/bonus5/effective99 becomes base99/effective99. An unchanged
base does not recompute a stale effective value. The last two source-helper
examples in the fixture are outside the admitted managed zero-bonus profile.

For the actor's initial hit accounting, byte counter 0xff becomes zero, the
unsigned-16 total accuracy and valuable-target flag start at zero, and the
candidate signed-16 priority starts at zero. For each included positive-accuracy
target/strike, `strikes_simulate` applies the target result, then credits **+3**
for a stat effect on its intended side (or **−4** for the wrong side).
Ordinary non-stat hits use +0/−1. The stat credit is independent of the amount
of PA gained and the result helper's outcome. Thus an included self hit at PA99
still earns +3. It increments hit count, totals accuracy and marks valuable
only if that target remains targetable. `action_hits_simulate` rejects an
initial candidate with no counted hit or no valuable target; otherwise it
stores integer average accuracy in byte base-hit-percent, **100** here.

The simulation marks Act used, applies the existing turn-end CT refund/cap,
then advances the source clock at least to its next event unless its source
limit/other effects intervene. With two living effect-free units and no charges,
the shared managed clock already models the next ready-unit upkeep draw.
That is a further gameplay draw, distinct from the earlier usefulness coin;
both origins are separately simulated/scored and retain their decision RNG.
This study does not claim an entire decision has only two draws: refresh,
other candidates, NonBlue reach adjustment and the lottery also consume RNG.
VSync/BIOS frame interleaving remains unresolved, as in the algorithm guide.

## Absolute score, rank and restoration

`rank_current_decision_clear` clears **only decided**, preserving strike credit.
`ai_action_simulate_and_score` then adds each compact unit's absolute state
contribution in source-slot order: integer HP*128/maxHP, conditional status
weights, 51 per unchanged non-monster equipment slot (including empty slots),
MP share and team Golem terms. Enemy contributions are negated; priority narrows
to signed16 after each addition. There is **no direct PA term**, no future
Attack damage estimate and no subtraction of the movement score from this
absolute score. In the closed MP0/Golem0/equipment-unchanged pair the equipment
terms cancel only after both units are scored.

Literal examples with maxHP50: full/full gives **3**; actor HP10 Critical versus
enemy HP50 gives **−132** (25−32−128+3); actor HP50 versus enemy HP10 Critical
gives **138**. Equal-state move-only scores would be 0, −135 and 135. PA5 and
PA99 have the same score in these otherwise identical cases. This is not a
general recommendation that the original always selects Accumulate.

`ai_action_evaluate` retains a strict improvement using decided, signed priority,
byte hit percent and then lower foe proximity. Complete ties preserve the
earlier candidate, including ACT-first when all keys tie. `ai_best_rank_insert`
requires beating the movement baseline by **rank_compare**, which excludes
proximity, then beating the last ranked slot by the stronger comparison before
ordered insertion. Proximity alone cannot pass the movement-baseline gate.
Reuse the existing rank/lottery machinery. The lottery takes rand()%100 per
visited slot, including empty ones; a first-slot hit100 candidate wins on its
first draw, after ranking against any other abilities already evaluated.

Baseline snapshots save/restore three base-attribute bytes and four effective-
attribute/CT bytes, HP/MP, status/death/team/equipment/position/choice/action
fields, and team Golem. Equipment-attribute bonuses are not copied here;
they are stable in the admitted profile. The simulation restores clock globals
on its normal/charged-action cleanup, and restores selected baseline fields
on completion/rejection. Suspension snapshots retain compact status separately;
they are not a general battle save. The source does not snapshot or rewind RNG,
nor the entire AI workspace. Managed queries continue to promise immutable
inputs and whole-query RNG rollback on failure, a separate bounded API contract.

## Required changes and selected child

Actual [Accumulate](../behavior/accumulate-implementation.md) already validates
explicit grants/effect bindings, consumes Act and publishes battle PA via
`ImmediateAbilityBattleSimulator`/`AccumulateFormula`. A later Attack reads
that battle value. That path is not an AI preview and must not be invoked to
manufacture command authority during scoring.

Current `AiProjectedUnit` carries HP/status/clock/geometry but no PA. Sword and
Throw Stone candidate types require distinct actor/target IDs and model damage
to the opponent. `AiCandidateState` always assigns target HP that way, so self
stat projection needs an explicit path. `AiCandidateScoring.ScoreFreshUnits`
starts from zero, sufficient for admitted damage candidates, but would lose
the source +3. Any shared extension must default existing paths to zero and
accept strike credit only from a fresh privately owned projection, never a
caller-populated score/result. Keep actor self-target separate from the hostile
unit ID used for scoring and facing. Store hypothetical PA explicitly in the
self-effect outcome without implying that it is a trusted live snapshot.

Select **ZOD-230 — Project and score one supplied Accumulate self candidate**:

- Admit one explicit learned, immediate PA+1 effect in the existing two-living-
  opponent, flat, zero-bonus, effect-free domain. Support either actor identity
  and both candidate orders. Input is a supplied candidate **after usefulness
  filtering**, with explicit active/valuable-self certification. Do not place
  the coin inside a per-candidate operation or infer retail AI flags from samples.
- Privately project PA through the shared actual arithmetic, choices, position,
  turn-end and existing hypothetical clock. Expose before/after PA diagnostics;
  preserve the input and retain the decision stream only on supported success.
- Derive +3 and hit100 from this closed effect, add fresh absolute score, and
  reuse ranking helpers without admitting a new action to pair searches yet.
  Cover PA1/98/99, unchanged HP/status, same/moved destinations, both orders,
  independent literal clock/RNG boundaries, negative absolute score, replay,
  input isolation and failed-domain/draw/tick rollback. Keep Attack/Stone and
  both admitted team paths unchanged, with regression checks.

After that child, separately select fresh list/preparation/usefulness traversal,
then search integration and Framework-owned command authority. Facing decision
context and continuations currently support Attack/targeted ACT/Wait/Move, not
self PA ACT; real integration must add an explicitly validated self-action
branch and fresh RNG verification through the existing transaction owner.
Never publish a hypothetical result or execute a public cached plan. No command
work is part of ZOD-230. General lists/rosters, broader statuses/bonuses, rewards,
host/editor activation and full-turn Accumulate behavior remain later work.
ZOD-106 remains deferred; boosted PA remains battle-only.

## Reproduction and validation limits

The [18 literal descriptions](../fixtures/behavior/ai-accumulate-contract-v1.json)
exercise classifier parity/early returns, PA cap/recompute/restoration,
absolute score and strict rank gates. The Python verifier checks those supplied
descriptions, source file/function identities and optionally the prepared data.
It is not a C# implementation test, a general classifier or retail execution.

```powershell
python tools/verification/verify_ai_accumulate_contract.py --reference C:/repositories/fft_decomp --prepared scratch/all-data-extraction/SCUS_942.21 --output scratch/accumulate-ai-229/source-audit.json
python tools/verification/verify_ai_algorithm_guide.py --reference C:/repositories/fft_decomp --output scratch/accumulate-ai-229/source-guide.json
python -m unittest discover -s tools/verification -p 'test_*.py'
```

On this workstation use the installed Python executable if the WindowsApps
`python` alias is unavailable. The full 539-test Python discovery passes with
Pillow 12.3.0 from `scratch/accumulate-ai-229/python-deps` on PYTHONPATH and
TEMP/TMP under `scratch/accumulate-ai-229/python-temp`; the bundled interpreter's
Pillow 13.0.0 triggers six existing explicit version guards. Both source audits
pass, including prepared data and the complete algorithm-guide inventory.
The previous 4,132 .NET validation was not rerun for this source-only milestone.
Validation results are also recorded in the handoff;
reports remain under ignored scratch. No original/emulator observation, C
execution, new managed behavior, presentation review or runtime-equivalence
claim accompanies this source study.
