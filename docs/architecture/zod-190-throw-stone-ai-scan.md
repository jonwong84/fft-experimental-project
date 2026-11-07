# Throw Stone origin and target scan — ZOD-190

Investigated 2026-10-29 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base
`adce90c`. The [source receipt](../evidence/ai/throw-stone-scan-contract-v1.json)
pins seven files / 28 functions and inherits the
[ability iteration receipt](zod-185-ai-ability-iteration.md).
The [seven isolated descriptions](../fixtures/behavior/ai-throw-stone-scan-contract-v1.json)
use supplied score/path/result facts. They do not execute C, the original or
managed AI, calculate projectile paths, or establish a full search RNG schedule.

## Entry, target and aim lifetime

Hash-checked prepared SCUS-94221 data confirms ability148, range4, AoE0,
vertical0, CT0, MP0, enemy/HP/physical/direct/evadeable behavior, CannotTargetSelf,
CannotHitCaster and NoFollowTarget. RandomUse, Reflectable, FollowTarget and
TargetMapTiles are absent. The raw records stay ignored.

`abilities.c::ai_unit_abilities_init` clears runtime usage's low three bits,
then skips their recomputation for a direct/arc ability. Thus Throw Stone has
no exhaustive-origin or force-consume bit. Static byte4 does not establish
runtime usage. `turn.c::ability_considerations_set` sets consume-aim because
`ai_target_classify_ability_effect(actor,1)` returns None at CannotHitCaster.
This applies even if CT0 does not meet the actor-delay comparison. Original
preparation/usability and source context still require certification in the
bounded model; an ability ID or grant does not derive every policy fact.

`ai_ability_outcome_evaluate` skips unusable entries before target work.
Admitted Throw Stone has no RandomUse roll. Active eligibility is copied;
`useful_targets_filter` keeps the ordinary living opponent and drops the caster.
Self-target, reflection, Math Skill, map-target and FollowTarget passes are
absent. CT0 makes `unit_can_move_by_charge_deadline` return0, so even an enemy
already at CT100 survives the tile-target opportunity filter. The entry sets
consume-evaluated-aim in the target-setting workspace before its tile pass.
The caster's effect is None, not Adverse; do not replace preparation's consume
policy with the later self-hit heuristic in `strikes_simulate`.

`ai_cast_aim_tiles_build` temporarily excludes the actor. With one eligible
target, it intersects the scenario origin mask with the target's range4/AoE0
diamond. Aim is the target panel, intersected with walkable terrain. Flat dry
lower ground eliminates the water/height/Geomancy/Monster Skill branches, but
does not alone establish original targetability, sprite collision or projectile
path. Masks/counts are fresh per pass; neither public projected outcomes nor
previous candidates may seed these masks.

## Origin selection, cast and consumption

`origin_reachable_evaluate` sets ranked-origin mode for every non-exhaustive
entry, regardless of the truncated aim-count × origin-count threshold. It calls
`origin_next_take` while both counts remain. The key is signed16 tile priority
×65536 plus unsigned8 foe proximity; lowest key wins. The movement grid stores
the negated absolute score. The key fits signed32 even at source width limits.
The scan visits layer/y/x ascending and replaces on `best_key >= key`, so later
coordinates win a complete origin-key tie. This differs from Attack's exhaustive
map-order scan and from retaining the earliest equally ranked action.

The key belongs to the cast origin. If it is the actor's original coordinates,
`origin_destination_finalize` sets ACT-first and substitutes the scenario's D1
as the final movement destination. Other origins are Move-first and remain the
final destination. Proximity for a later action-rank tie belongs to the final
destination; origin ordering must not silently use that substituted value.

`origin_aim_tiles_evaluate` places the actor at the cast origin, rebuilds actual
ability range, intersects remaining aim panels and visits layer/y/x ascending.
It finds a present unit on the panel and checks a direct projectile even at
distance1: Throw Stone is a default ability, not Item's adjacent exception.
`projectile_direct_check_between_units` raises endpoints by sprite height and
traces against terrain/other units. No RNG is drawn by these path checks.
An obstructed path skips action evaluation and retains aim for another origin.

For a clear eligible panel, `consume_aim_tile` is set before fresh action
evaluation. Four evasion draws occur even at zero accuracy; NotValuable still
sets ability-processing-done and reaches aim removal. The sole aim is consumed
after Scored or NotValuable, so no second clear origin is evaluated. Zero
accuracy is not a reason to retry a more favorable angle. Supported projection
restores original units and retains its private RNG. Fresh best reset is
marker0/priority-32767/hit0; candidate ranking and later ranked-list insertion
are distinct. Original C does not rewind a partially completed search on a
managed unsupported/exhaustion failure.

## Selected child: ZOD-191

Implement a pure supplied-scenario query for a prepared, usable Throw Stone
entry with one ordinary opponent and explicitly certified clear direct paths.
Own the fresh movement-scoring prefix in layer/y/x order, derive signed origin
keys and foe proximity from the same original state, select the first ranked
in-range origin, and own one fresh candidate/baseline comparison. D0, D1 and
the original scoring/scenario masks remain caller-certified; no cached score or
projected outcome inputs. Carry prefix RNG into the candidate once, without
rescoring D0. Inactive/no-origin results retain prefix cost; NotValuable retains
four evasion draws and never retries. Reject partial work atomically.

Certify source masks, baseline provenance, fresh decision/best context, prepared
usage, no intervening random work and clear direct trajectories with named
booleans. Existing grants/geometry/score/clock admission still applies. Check
ACT-first substitution, Move-first, range4/5, later-coordinate origin ties,
lower proximity, exact/late budgets, KO, replay and collection-order independence.
Reuse existing formula/clock/score and movement internals; preserve Attack APIs.
Do not implement obstructed-path traversal, arbitrary teams/statuses, list
cursor/ranked insertion/lottery, live authority, broad interface or host/editor
activation in this child.

## Reproduction

Run `verify_ai_throw_stone_scan_contract.py --reference C:/repositories/fft_decomp
--prepared-executable scratch/all-data-extraction/SCUS_942.21 --output <new scratch report>`.
It checks fingerprints, inherited receipts, descriptions and optional selected
semantic data. Three Python tests cover authored literals/input isolation,
malformed descriptions and tampering. All 522 Python tests and the clean
source/prepared-data audit pass; logs are in ignored
`scratch/ai-throw-stone-scan-190-191/`. No new managed code belongs to ZOD-190;
the last managed validation is ZOD-189's clean build and 4,006 passing cases.
