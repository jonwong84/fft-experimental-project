# Ordinary spell movement origins — ZOD-426 source study

This study traces the movement callers around the existing
[current-origin spell evaluation](ordinary-spell-aim-ranking.md). It closes a
source milestone and selects ZOD-437, fresh ordinary-spell movement baseline
diagnostics. It adds no runtime search, command authority or autonomous magic.

The subsequent [ZOD-437 implementation](ordinary-spell-movement-baselines.md)
now derives bounded fresh offensive movement baselines and priority writes.
Cast-origin enumeration and moved-origin spell evaluation remain separate.

The managed baseline is `700276a426b622d50d5075300c6aff049cd680da`. The clean,
read-only decompilation is pinned to
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/spell-movement-origins-source-v1.json) records
normalized file/function fingerprints and prepared BATTLE byte checks. Neither
reference C nor the original executable was run.

## Routine flow and ownership

`general_select` builds movement scenarios, seeds the movement baseline from
scenario 0's chosen destination D0, then evaluates ability alternatives under
scenario 1 and the complementary scenario as appropriate. Baseline D0 and a
scenario's eventual movement destination are separate values.

For ordinary non-self targets, `ai_ability_outcome_evaluate` retains the existing
unit-before-tile pass order. Each pass reaches:

1. `origin_reachable_evaluate` builds pass-specific cast/aim masks, temporarily
   excluding the actor from valuable targets, and chooses the origin scan mode.
2. `origin_next_take` selects and removes a ranked origin; exhaustive scans
   instead visit layer, Y, X ascending directly.
3. `origin_destination_finalize` selects ACT-before-MOVE or MOVE-before-ACT and
   checks the resulting destination's pickup classification.
4. `origin_aim_tiles_evaluate` temporarily places the actor at the cast origin,
   intersects the remaining aims with that origin's selectable range, and calls
   the existing candidate simulation/ranking flow.
5. On normal completion, `ai_acting_unit_place` restores the actor's cached
   X/Y/layer. This restores coordinates, not RNG or the search's masks/scores.

The pinned BATTLE counterparts are `origin_reachable_evaluate` at 0x8019BF2C,
`origin_destination_finalize` at 0x8019C20C, `origin_next_take` at 0x8019C280,
and `rank_baseline_seed` at 0x8019630C. Supporting function fingerprints cover
the callers, masks, scenario selection, rank comparison and strike simulation.

## Ranked origins and source widths

An exhaustive-origin flag clears the consume-evaluated-aims setting and uses
the ascending map scan. Otherwise the original calculates an unsigned-byte
aim-count times origin-count product and tests whether it exceeds 10, but then
**unconditionally sets ranked-origin scanning**. Small products also use ranked
origins. The prepared instructions include the unconditional store at 0x8019C064;
the receipt pins that instruction and the surrounding branch/delay slot.

Ranked origins minimize the signed 32-bit key
`signed 16-bit tile priority * 65536 + unsigned 8-bit foe proximity`. Later scanned
coordinates replace an equal key, so exact ties visit higher layer, then higher
Y, then higher X first. The chosen bit is XOR-removed and the origin byte count
decrements. The helper has no empty-state guard.

`ai_flagged_tiles_count` counts set bits as signed 32-bit, but the caller stores aim
and origin counts as unsigned bytes. A nonempty 256-cell mask can therefore have
count 0. The ranked loop tests both byte counts and can skip it; exhaustive
scanning reads mask rows directly. The source descriptions preserve this alias
without broadening current runtime admission. A future initial runtime domain
should certify positive counts at most 255 rather than reinterpret zero.

## Cast origin, destination and masks

The actor-origin test compares the entire coordinate word, including layer and
the fourth byte. If equal, it sets ACT-before-MOVE and substitutes the current
scenario's destination in the action record. Candidate simulation still casts
from the cached actor origin. Other origins set MOVE-before-ACT and also become
the action destination. The original normally initializes the fourth byte to 0;
a typed adapter must certify that boundary rather than compare X/Y alone.

Origin visitation ranks the selected origin **before** destination substitution.
Later best-candidate proximity comparison reads the final action destination.
Those keys must remain distinct when casting from the current tile and moving
elsewhere afterward.

For the ordinary branch, `ai_cast_aim_tiles_build` intersects the selected
scenario's reachable-origin mask with masks around valuable non-actor targets:
range for unit targeting, range plus area for tile targeting. Aim masks are
constructed separately, at target tiles or area centers around them, on both
source layers. FollowTarget tile passes and jumping targets XOR-remove the
target's own-layer tile. The walkable mask filters **aims**, independently of
the actor's movement reach. An occupied target outside Move can be a valid aim.

Current safe radius masks remain 0–7. The original table overread at 8–15 and
whole-map branch from 16 are documented boundaries, not new runtime support.
Deep water, arcing, Geomancy and Monster Skill filters need their own admission.
Two-layer descriptions document source masks; existing ordinary runtime remains
bounded to its certified dry, flat lower-layer geometry and supported occupants.
Ordinary movement distance uses propagated budget differences; Manhattan
distance is a separate unmounted-Teleport branch. Flat cost 1 equality does not
justify generalizing to slopes, obstacles, mounts or special movement.

## Remaining aims, exits and RNG

Each origin scans range-intersected remaining aims in layer/Y/X order.
`ai_action_evaluate` compares a candidate before the caller checks
`ability_processing_done`. An unresolved candidate can rank with hit 255, then
stop only this origin **before** aim consumption. Later origins may reconsider
that aim with the same evolving decision RNG and best rank.

After a completed evaluation the current consume flag removes the aim globally
for that pass. Ranked strike simulation can also force consumption: Useful self
effect with a self hit, or Adverse self effect without a self hit. Thus a fixed
consume setting alone is insufficient for multi-origin search. The next caller
unit/tile pass rebuilds its masks; candidate RNG and best-rank ownership follow
the enclosing ability evaluation.

Missing-target and trajectory branches have earlier skips and a potentially
retained consume flag. The supplied outcome tapes exclude those branches.
Frame suspension/resumption also retains scan phase and requires a separate
continuation contract. Neither branch is approximated by the tape verifier.

`ai_move_origins_evaluate` is a distinct map/self-target caller: evaluate an
allowed actor origin, then an allowed distinct movement destination. Its self
helper checks the original actor origin for both range passes, and a suspended
first evaluation can skip the moved pass on resume. Do not impose this two-origin
pattern on the ordinary reachable-origin loop or silently normalize its quirks.

## Baseline and priority provenance

`rank_baseline_seed` initializes decided 0 and hit 100. Priority is a signed 16-bit
store of the negated **unsigned 16-bit load** at D0's tile. Stored -32768 consequently
negates back to -32768 after narrowing. `rank_compare` compares decided byte,
signed priority, then hit byte. Better foe proximity alone cannot pass an equal
movement baseline; hit 255 can beat hit 100 at equal priority. Best-candidate and
ranked-list comparisons have their separately documented destination-proximity
tie handling; exact best ties retain the earlier candidate.

Scenario 2 is complemented before the actor tile is reinserted into scenario 1.
An unscored actor origin can consequently belong to both scenarios. Fresh scored
tiles carry explicit writes; other cells require retained priority provenance.
Unknown workspace values must remain unknown. Existing `AiPriorityWorkspace`,
`AiScenarioLayout` and `AiRosterCastGeometry.BindRanked` provide bounded physical
precedents; importing their assumptions wholesale would not prove spell support.

## Representation and selected implementation child

Use named booleans for understood independent decisions such as exhaustive
origins, consume aim, ACT-before-MOVE and processing completion. Existing
`DefaultAbilityAiUsage` already follows that rule. Static metadata and mutable
working usage retain separate owners. Counts, rank values, coordinates and
multi-state phase fields retain their numeric/typed meanings; unknown bits stay
opaque at the reference/import boundary. Owner toggle drafts and immutable
JobStats are unchanged.

ZOD-437 will derive fresh, definition-bound ordinary-spell movement baseline
diagnostics using existing preparation, isolated movement simulation, clocks
and absolute scoring. Its output must distinguish D0 and scenario destinations,
fresh priority writes and unread/unknown cells, and carry one decision RNG.
Keep named source-routine methods and certify a dry flat lower-layer domain
before evaluating multiple cast origins. Retained caller workspaces, moved-origin
casting, insertion/lottery, commands, autonomous magic, broad statuses/Reflect/
Golem, terminal/frame RNG, editor and custom cone work remain separate.

## Validation

The [fixture](../fixtures/behavior/spell-movement-origins-v1.json) contains 24
literal descriptions: scan policies/order, full-word destinations, baseline
widths/admission, distinct masks and supplied candidate iteration. Eight Python
tests check those descriptions, mutation detection and input isolation. Supplied
candidate outcomes are not a formula or original-execution oracle. Cross-origin
tapes retain 9 draws/seed 1040713558 or 264 draws/seed 1800344689 from seed 57; equal
rank retention has 8 draws/seed 361004401. Affine LCG jump calculations independently
cross-check those checkpoints.

Reproduce with Python 3 and the clean pinned reference:

```powershell
python tools/verification/verify_spell_movement_origins.py --reference C:/repositories/fft_decomp --battle-bin scratch/fft-decomp-work/build/extracted/files/BATTLE.BIN
python -m unittest discover -s tools/verification -p test_spell_movement_origins.py
```

The optional prepared input is local evidence only. No original data is committed
or read by the game at runtime. See the receipt for final checks and preservation.
